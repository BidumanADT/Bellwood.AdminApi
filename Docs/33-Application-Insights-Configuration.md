# Application Insights Configuration

**Phase 3**: Production Monitoring & Alerting  
**Last Updated**: January 18, 2026

---

## Overview

This document describes how to configure Application Insights for the Bellwood AdminAPI to enable production-grade monitoring, alerting, and diagnostics.

---

## Configuration

### appsettings.json

Add Application Insights connection string to your configuration:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key-here;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/;LiveEndpoint=https://your-region.livediagnostics.monitor.azure.com/"
  }
}
```

### Environment Variables (Recommended for Production)

```bash
# Windows
$env:ApplicationInsights__ConnectionString = "InstrumentationKey=...;"

# Linux/macOS
export ApplicationInsights__ConnectionString="InstrumentationKey=...;"
```

---

## Azure Setup

### Step 1: Create Application Insights Resource

```bash
# Login to Azure
az login

# Create Application Insights resource
az monitor app-insights component create \
  --app bellwood-adminapi \
  --location eastus \
  --resource-group BellwoodAPI \
  --application-type web

# Get connection string
az monitor app-insights component show \
  --app bellwood-adminapi \
  --resource-group BellwoodAPI \
  --query connectionString \
  --output tsv
```

### Step 2: Configure in App Service

```bash
az webapp config appsettings set \
  --name bellwood-adminapi \
  --resource-group BellwoodAPI \
  --settings ApplicationInsights__ConnectionString="your-connection-string"
```

---

## Monitored Metrics

### Automatic Tracking

Application Insights automatically tracks:

- **HTTP Requests**: All incoming requests with duration, status codes
- **Dependencies**: External API calls (future: LimoAnywhere integration)
- **Exceptions**: Unhandled exceptions with stack traces
- **Performance Counters**: CPU, memory, request rate

### Custom Tracking

The ErrorTrackingMiddleware adds:

1. **Error Tracking**:
   - 4xx errors (client errors)
   - 5xx errors (server errors)
   - Unauthorized (401) attempts
   - Forbidden (403) attempts with security alerts

2. **Security Events**:
   - Excessive forbidden attempts (>10/hour) - **SECURITY ALERT**
   - Invalid role changes
   - Repeated authorization failures

3. **Performance Metrics**:
   - Slow requests (>2 seconds)
   - Request duration distribution
   - Throughput (requests/second)

---

## Alerts Configuration

### Recommended Alerts

#### 1. Excessive Forbidden (403) Attempts

**Trigger**: More than 10 forbidden attempts from same user/IP in 1 hour

**Query**:
```kusto
customEvents
| where name == "SecurityAlert.ExcessiveForbiddenAttempts"
| where timestamp > ago(1h)
| summarize count() by tostring(customDimensions.UserId), tostring(customDimensions.IpAddress)
| where count_ >= 10
```

**Action**: Email security team, block IP if continues

---

#### 2. High Server Error Rate

**Trigger**: More than 5% of requests return 5xx in 5 minutes

**Query**:
```kusto
requests
| where timestamp > ago(5m)
| summarize TotalRequests = count(), ServerErrors = countif(resultCode startswith "5")
| extend ErrorRate = (ServerErrors * 100.0) / TotalRequests
| where ErrorRate > 5
```

**Action**: Email DevOps team, investigate logs

---

#### 3. Slow Request Performance

**Trigger**: Average request duration >1 second for 10 minutes

**Query**:
```kusto
requests
| where timestamp > ago(10m)
| summarize AvgDuration = avg(duration)
| where AvgDuration > 1000
```

**Action**: Email DevOps team, check server load

---

#### 4. High Memory Usage

**Trigger**: Memory usage >500MB

**Query**:
```kusto
customMetrics
| where name == "MemoryUsedMB"
| where value > 500
```

**Action**: Email DevOps team, consider scaling up

---

#### 5. Unhandled Exceptions

**Trigger**: Any unhandled exception occurs

**Query**:
```kusto
exceptions
| where timestamp > ago(5m)
| where outerMessage contains "Unhandled"
```

**Action**: Email DevOps team immediately, investigate logs

---

## Health Check Endpoints

### GET /health

**Purpose**: Basic health check (backward compatibility)

**Response**:
```json
{
  "status": "ok"
}
```

---

### GET /health/live

**Purpose**: Kubernetes liveness probe - "Is the app running?"

**Response** (Healthy):
```json
{
  "status": "Healthy",
  "timestamp": "2026-01-18T12:00:00Z",
  "duration": 45.2,
  "checks": [
    {
      "name": "AdminAPI",
      "status": "Healthy",
      "description": "All systems operational",
      "duration": 45.2,
      "data": {
        "BookingRepository": "OK",
        "QuoteRepository": "OK",
        "AuditLogRepository": "OK",
        "DataProtection": "OK",
        "SignalR": "OK",
        "MemoryUsedMB": 125,
        "UptimeMinutes": 1440
      }
    }
  ]
}
```

**Response** (Unhealthy):
```json
{
  "status": "Unhealthy",
  "timestamp": "2026-01-18T12:00:00Z",
  "duration": 52.8,
  "checks": [
    {
      "name": "AdminAPI",
      "status": "Unhealthy",
      "description": "System unhealthy: BookingRepository not available, High memory usage: 520MB",
      "duration": 52.8,
      "data": {
        "BookingRepository": "FAIL",
        "MemoryUsedMB": 520
      }
    }
  ]
}
```

---

### GET /health/ready

**Purpose**: Kubernetes readiness probe - "Can the app serve traffic?"

Same format as `/health/live`, but includes dependency checks.

---

## Kubernetes Configuration

### deployment.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: bellwood-adminapi
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: adminapi
        image: bellwood/adminapi:latest
        ports:
        - containerPort: 80
        env:
        - name: ApplicationInsights__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-insights-secret
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health/live
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
```

---

## Viewing Telemetry

### Azure Portal

1. Navigate to Application Insights resource
2. **Live Metrics**: Real-time request/error monitoring
3. **Failures**: View failed requests and exceptions
4. **Performance**: Analyze slow requests
5. **Logs**: Query custom events and metrics

### Example Queries

**View all security alerts**:
```kusto
customEvents
| where name startswith "SecurityAlert"
| project timestamp, name, customDimensions
| order by timestamp desc
```

**Top 10 slowest endpoints**:
```kusto
requests
| summarize AvgDuration = avg(duration), Count = count() by name
| order by AvgDuration desc
| take 10
```

**Error rate by endpoint**:
```kusto
requests
| summarize Total = count(), Errors = countif(success == false) by name
| extend ErrorRate = (Errors * 100.0) / Total
| order by ErrorRate desc
```

---

## Local Development

For local development without Application Insights:

1. Leave `ApplicationInsights:ConnectionString` empty or omit from appsettings.Development.json
2. Telemetry will be collected but not sent
3. Logs will still appear in console

---

## Troubleshooting

### Telemetry Not Appearing

**Check**:
1. Connection string is valid
2. Network allows outbound HTTPS to Azure
3. Check logs for Application Insights errors

**Test Connection**:
```bash
curl -X POST https://dc.services.visualstudio.com/v2/track \
  -H "Content-Type: application/json" \
  -d '{"name":"Test","time":"2026-01-18T12:00:00Z","iKey":"your-instrumentation-key"}'
```

### High Costs

Application Insights charges based on data ingestion. To reduce costs:

1. Use sampling (default: 100% sampling)
2. Filter noisy telemetry (health checks, static files)
3. Set retention period (default: 90 days)

---

## Related Documentation

- `30-Deployment-Guide.md` - Deployment instructions
- `32-Troubleshooting.md` - Common issues & solutions
- [Application Insights Documentation](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)

---

**Last Updated**: January 18, 2026  
**Phase**: 3B - Monitoring & Alerting  
**Status**: ? Production Ready
