# Bellwood AdminAPI Documentation Index

**Last Updated**: January 14, 2026  
**Version**: 2.0.0 - Consolidated Documentation Structure

---

## ?? Documentation Overview

This documentation library is organized into clear categories for easy navigation. Each document is a **living document** that will be updated as features evolve.

---

## ?? Quick Start

| Document | Purpose | Audience |
|----------|---------|----------|
| `README.md` (root) | Complete API overview & getting started | Everyone |
| `01-System-Architecture.md` | System integration & component overview | Developers, Architects |
| `02-Testing-Guide.md` | Testing workflows & scripts | QA, Developers |

---

## ?? Main Documentation (Living Documents)

### Architecture & Integration

| Document | Description |
|----------|-------------|
| `01-System-Architecture.md` | Complete system architecture, UserUid flow, all 5 components |
| `02-Testing-Guide.md` | Hybrid testing workflow, Phase 1 tests, troubleshooting |

### Feature Implementation

| Document | Description |
|----------|-------------|
| `10-Real-Time-Tracking.md` | Complete GPS tracking: SignalR, location endpoints, privacy |
| `11-User-Access-Control.md` | Phase 1 implementation: ownership, RBAC, authorization |
| `12-Timezone-Support.md` | Worldwide timezone handling, DateTime fixes, DateTimeOffset |
| `13-Driver-Integration.md` | Driver endpoints, affiliate management, assignment system |
| `14-Passenger-Tracking.md` | Passenger location endpoint, email authorization, safety |

### Technical References

| Document | Description |
|----------|-------------|
| `20-API-Reference.md` | Complete endpoint documentation with examples |
| `21-SignalR-Events.md` | SignalR hub methods, events, groups, subscriptions |
| `22-Data-Models.md` | Entity models, DTOs, ownership fields |
| `23-Security-Model.md` | JWT auth, role-based access, authorization matrix |

### Deployment & Operations

| Document | Description |
|----------|-------------|
| `30-Deployment-Guide.md` | Build, publish, environment setup, production checklist |
| `31-Scripts-Reference.md` | PowerShell test scripts documentation |
| `32-Troubleshooting.md` | Common issues, solutions, diagnostic steps |

### Project Management

| Document | Description |
|----------|-------------|
| `CHANGELOG.md` | Version history and release notes |
| `ROADMAP.md` | Future enhancements and feature planning |

---

## ??? Archive

Historical documents preserved in `Archive/` folder for reference:

### Superseded by Living Documents

| Archived Document | Replaced By |
|-------------------|-------------|
| `COMPLETE_SOLUTION_SUMMARY.md` | `10-Real-Time-Tracking.md` |
| `FINAL_COMPLETE_SOLUTION.md` | `10-Real-Time-Tracking.md` |
| `FINAL_IMPLEMENTATION_SUMMARY.md` | `10-Real-Time-Tracking.md` |
| `BELLWOOD_SYSTEM_INTEGRATION.md` | `01-System-Architecture.md` |
| `DRIVER_API_SUMMARY.md` | `13-Driver-Integration.md` |
| `PASSENGER_LOCATION_TRACKING_GUIDE.md` | `14-Passenger-Tracking.md` |
| `REALTIME_TRACKING_BACKEND_SUMMARY.md` | `10-Real-Time-Tracking.md` |

### Individual Fix Documents

| Archived Document | Consolidated Into |
|-------------------|-------------------|
| `ADMINPORTAL_LOCATION_FIX.md` | `32-Troubleshooting.md` |
| `ADMINPORTAL_FIX_TECHNICAL_SUMMARY.md` | `32-Troubleshooting.md` |
| `BOOKING_LIST_ENHANCEMENT_SUMMARY.md` | `10-Real-Time-Tracking.md` |
| `DATETIMEKIND_FIX_SUMMARY.md` | `12-Timezone-Support.md` |
| `DATETIME_OFFSET_BUG_FIX.md` | `12-Timezone-Support.md` |
| `DRIVER_STATUS_TIMEZONE_FIX_SUMMARY.md` | `10-Real-Time-Tracking.md` + `12-Timezone-Support.md` |
| `TIMEZONE_FIX_DRIVER_RIDES_SUMMARY.md` | `12-Timezone-Support.md` |
| `FILE_REPOSITORY_RACE_CONDITION_FIX.md` | `32-Troubleshooting.md` |
| `PASSENGER_TRACKING_ACTIVE_FIX.md` | `14-Passenger-Tracking.md` |
| `DRIVER_ASSIGNMENT_FIX_SUMMARY.md` | `13-Driver-Integration.md` |

### Migration & Update Guides

| Archived Document | Purpose |
|-------------------|---------|
| `BOOKING_LIST_MIGRATION_GUIDE.md` | Superseded by main docs |
| `DRIVER_APP_TIMEZONE_FIX_INTEGRATION.md` | Superseded by main docs |
| `DRIVER_APP_TIMEZONE_INTEGRATION.md` | Superseded by main docs |
| `README_UPDATE_FINAL.md` | Applied to root README.md |
| `README_UPDATE_SUMMARY.md` | Applied to root README.md |
| `UPDATE_SUMMARY.md` | Superseded by CHANGELOG.md |

### Administrative

| Archived Document | Purpose |
|-------------------|---------|
| `QUICK_REFERENCE_CHANGES.md` | Historical reference |
| `QUICK_REFERENCE_FINAL.md` | Historical reference |
| `IMPLEMENTATION_SUMMARY_CRITICAL_FIXES.md` | Historical reference |
| `PHASE1_CRITICAL_FIXES.md` | Historical reference |
| `ADMINPORTAL_INTEGRATION_GUIDE.md` | Consolidated into main docs |
| `AFFILIATE_DRIVER_SUMMARY.md` | Consolidated into `13-Driver-Integration.md` |
| `CREATED_ON_TIMEZONE_FIX.md` | Consolidated into `12-Timezone-Support.md` |
| `EXIT_CODE_FIX.md` | Consolidated into `32-Troubleshooting.md` |
| `PORT_UPDATE.md` | Consolidated into `30-Deployment-Guide.md` |
| `SCRIPTS_SUMMARY.md` | Consolidated into `31-Scripts-Reference.md` |

### Planning Documents

| Archived Document | Status |
|-------------------|--------|
| `Planning-DataAccessEnforcement.md` | Implemented in Phase 1 |
| `PHASE1_DATA_ACCESS_IMPLEMENTATION.md` | Implemented |
| `PHASE1_TESTING_GUIDE.md` | Replaced by `02-Testing-Guide.md` |
| `HYBRID_TESTING_WORKFLOW.md` | Replaced by `02-Testing-Guide.md` |

---

## ?? Document Naming Convention

### Prefixes

- `00-` - Index/Meta documents
- `01-09` - Core Architecture & Integration
- `10-19` - Feature Implementation
- `20-29` - Technical References
- `30-39` - Deployment & Operations
- `CHANGELOG.md` - Version history
- `ROADMAP.md` - Future planning

### Style

- **Descriptive names** with hyphens (e.g., `Real-Time-Tracking.md`)
- **Living documents** updated continuously
- **No version numbers** in filenames (use git history)
- **No dates** in filenames (use git commits)

---

## ?? Maintenance

### Updating Documentation

1. **Find the right document** using this index
2. **Edit in place** (don't create duplicates)
3. **Update "Last Updated" date** at top of document
4. **Commit changes** with descriptive message

### Creating New Documentation

1. **Check if topic exists** in current docs
2. **Add to existing document** if closely related
3. **Create new document** only if fundamentally different
4. **Follow naming convention**
5. **Update this index**

### Archiving Documents

1. **Move to Archive folder** when superseded
2. **Update this index** with archive mapping
3. **Add redirect note** to archived document
4. **Keep for historical reference** (don't delete)

---

## ?? Documentation Statistics

- **Main Documents**: 13
- **Archived Documents**: 37
- **Total Pages**: ~150
- **Total Words**: ~50,000
- **Last Major Reorganization**: January 14, 2026

---

## ?? Quick Links by Role

### New Developers

1. Start with `README.md` (root)
2. Read `01-System-Architecture.md`
3. Review `20-API-Reference.md`
4. Run `02-Testing-Guide.md` workflows

### QA/Testers

1. Read `02-Testing-Guide.md`
2. Review `31-Scripts-Reference.md`
3. Check `32-Troubleshooting.md`

### Mobile Developers

1. Read `01-System-Architecture.md`
2. Review `14-Passenger-Tracking.md`
3. Check `13-Driver-Integration.md`
4. Review `21-SignalR-Events.md`

### DevOps/Operations

1. Read `30-Deployment-Guide.md`
2. Review `32-Troubleshooting.md`
3. Check `CHANGELOG.md`

---

**Organized with ?? for the Bellwood development team**
