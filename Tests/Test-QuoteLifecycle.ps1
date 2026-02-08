# Quote Lifecycle Test Module
# PowerShell 5.1 Compatible
# Tests the complete quote lifecycle flow

#Requires -Version 5.1

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AdminToken,
    [switch]$Verbose
)

function Test-QuoteLifecycle {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  QUOTE LIFECYCLE INTEGRATION TEST" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    # Step 1: Create a quote
    Write-Host "Step 1: Creating new quote..." -ForegroundColor Yellow
    
    $quoteDraft = @{
        booker = @{
            firstName = "Integration"
            lastName = "Test"
            phoneNumber = "555-TEST-001"
            emailAddress = "integration.test@example.com"
        }
        passenger = @{
            firstName = "Test"
            lastName = "Passenger"
            phoneNumber = "555-TEST-002"
            emailAddress = "test.passenger@example.com"
        }
        vehicleClass = "Sedan"
        pickupDateTime = (Get-Date).AddDays(3).ToString("yyyy-MM-ddTHH:mm:ssZ")
        pickupLocation = "Integration Test Pickup Location"
        pickupStyle = 0  # Curbside
        dropoffLocation = "Integration Test Dropoff Location"
        passengerCount = 2
        checkedBags = 1
        carryOnBags = 1
    }
    
    $headers = @{
        "Authorization" = "Bearer $AdminToken"
        "Content-Type" = "application/json"
    }
    
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/quotes" `
            -Headers $headers -Body ($quoteDraft | ConvertTo-Json -Depth 10) -UseBasicParsing
        
        $quoteId = ($response.Content | ConvertFrom-Json).id
        Write-Host "? Quote created: $quoteId" -ForegroundColor Green
        
        # Step 2: Verify quote is in Pending status
        Write-Host "`nStep 2: Verifying quote status..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method GET -Uri "$AdminApiUrl/quotes/$quoteId" `
            -Headers $headers -UseBasicParsing
        
        $quote = $response.Content | ConvertFrom-Json
        
        if ($quote.status -eq "Pending") {
            Write-Host "? Quote status: Pending" -ForegroundColor Green
        } else {
            Write-Host "? Unexpected status: $($quote.status)" -ForegroundColor Red
            return $false
        }
        
        # Step 3: Acknowledge quote
        Write-Host "`nStep 3: Acknowledging quote..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/quotes/$quoteId/acknowledge" `
            -Headers $headers -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.status -eq "Acknowledged") {
            Write-Host "? Quote acknowledged successfully" -ForegroundColor Green
            Write-Host "  Acknowledged by: $($result.acknowledgedBy)" -ForegroundColor Gray
            Write-Host "  Acknowledged at: $($result.acknowledgedAt)" -ForegroundColor Gray
        } else {
            Write-Host "? Acknowledgment failed" -ForegroundColor Red
            return $false
        }
        
        # Step 4: Respond to quote with price/ETA
        Write-Host "`nStep 4: Responding to quote..." -ForegroundColor Yellow
        
        $responseBody = @{
            estimatedPrice = 125.50
            estimatedPickupTime = (Get-Date).AddDays(3).AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            notes = "Integration test response - vehicle confirmed"
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/quotes/$quoteId/respond" `
            -Headers $headers -Body ($responseBody | ConvertTo-Json) -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.status -eq "Responded") {
            Write-Host "? Quote responded successfully" -ForegroundColor Green
            Write-Host "  Price: `$$($result.estimatedPrice)" -ForegroundColor Gray
            Write-Host "  Pickup time: $($result.estimatedPickupTime)" -ForegroundColor Gray
            Write-Host "  Notes: $($result.notes)" -ForegroundColor Gray
        } else {
            Write-Host "? Response failed" -ForegroundColor Red
            return $false
        }
        
        # Step 5: Accept quote (creates booking)
        Write-Host "`nStep 5: Accepting quote (creates booking)..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/quotes/$quoteId/accept" `
            -Headers $headers -UseBasicParsing
        
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.quoteStatus -eq "Accepted" -and $result.bookingId) {
            Write-Host "? Quote accepted successfully" -ForegroundColor Green
            Write-Host "  Quote status: $($result.quoteStatus)" -ForegroundColor Gray
            Write-Host "  Booking created: $($result.bookingId)" -ForegroundColor Gray
            Write-Host "  Booking status: $($result.bookingStatus)" -ForegroundColor Gray
            Write-Host "  Source quote: $($result.sourceQuoteId)" -ForegroundColor Gray
        } else {
            Write-Host "? Acceptance failed" -ForegroundColor Red
            return $false
        }
        
        # Step 6: Verify booking was created correctly
        Write-Host "`nStep 6: Verifying booking details..." -ForegroundColor Yellow
        
        $bookingId = $result.bookingId
        $response = Invoke-WebRequest -Method GET -Uri "$AdminApiUrl/bookings/$bookingId" `
            -Headers $headers -UseBasicParsing
        
        $booking = $response.Content | ConvertFrom-Json
        
        if ($booking.sourceQuoteId -eq $quoteId) {
            Write-Host "? Booking linked to quote correctly" -ForegroundColor Green
            Write-Host "  Booking ID: $($booking.id)" -ForegroundColor Gray
            Write-Host "  Source Quote ID: $($booking.sourceQuoteId)" -ForegroundColor Gray
            Write-Host "  Status: $($booking.status)" -ForegroundColor Gray
        } else {
            Write-Host "? Booking not linked to quote" -ForegroundColor Red
            return $false
        }
        
        Write-Host "`n? COMPLETE LIFECYCLE TEST PASSED" -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "? LIFECYCLE TEST FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
        return $false
    }
}

# Run test if called directly
if ($AdminToken) {
    $result = Test-QuoteLifecycle
    if (-not $result) {
        exit 1
    }
} else {
    Write-Host "Error: AdminToken parameter required" -ForegroundColor Red
    exit 1
}
