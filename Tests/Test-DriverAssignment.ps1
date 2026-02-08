# Driver Assignment Test Module
# PowerShell 5.1 Compatible
# Tests driver assignment and ride workflow

#Requires -Version 5.1

param(
    [string]$AdminApiUrl = "https://localhost:5206",
    [string]$AdminToken,
    [switch]$Verbose
)

function Test-DriverAssignment {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  DRIVER ASSIGNMENT INTEGRATION TEST" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    $headers = @{
        "Authorization" = "Bearer $AdminToken"
        "Content-Type" = "application/json"
    }
    
    try {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        
        # Step 1: Create affiliate
        Write-Host "Step 1: Creating test affiliate..." -ForegroundColor Yellow
        
        $affiliateBody = @{
            name = "Test Transport Services"
            pointOfContact = "Test Manager"
            phone = "555-AFFILIATE"
            email = "test.affiliate@example.com"
            streetAddress = "123 Test Street"
            city = "Chicago"
            state = "IL"
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/affiliates" `
            -Headers $headers -Body ($affiliateBody | ConvertTo-Json) -UseBasicParsing
        
        $affiliate = $response.Content | ConvertFrom-Json
        $affiliateId = $affiliate.id
        Write-Host "? Affiliate created: $affiliateId" -ForegroundColor Green
        
        # Step 2: Create driver under affiliate
        Write-Host "`nStep 2: Creating test driver..." -ForegroundColor Yellow
        
        $driverBody = @{
            name = "Test Driver Charlie"
            phone = "555-DRIVER-001"
            userUid = "test-driver-charlie-001"
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/affiliates/$affiliateId/drivers" `
            -Headers $headers -Body ($driverBody | ConvertTo-Json) -UseBasicParsing
        
        $driver = $response.Content | ConvertFrom-Json
        $driverId = $driver.id
        Write-Host "? Driver created: $driverId" -ForegroundColor Green
        Write-Host "  Name: $($driver.name)" -ForegroundColor Gray
        Write-Host "  UserUid: $($driver.userUid)" -ForegroundColor Gray
        
        # Step 3: Create a booking
        Write-Host "`nStep 3: Creating test booking..." -ForegroundColor Yellow
        
        $bookingDraft = @{
            booker = @{
                firstName = "Driver"
                lastName = "Test"
                phoneNumber = "555-BOOKER"
                emailAddress = "driver.test@example.com"
            }
            passenger = @{
                firstName = "Passenger"
                lastName = "TestRide"
                phoneNumber = "555-PASSENGER"
                emailAddress = "passenger.testride@example.com"
            }
            vehicleClass = "Sedan"
            pickupDateTime = (Get-Date).AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ")
            pickupLocation = "Test Pickup - Union Station"
            pickupStyle = 0
            dropoffLocation = "Test Dropoff - O'Hare Airport"
            passengerCount = 1
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/bookings" `
            -Headers $headers -Body ($bookingDraft | ConvertTo-Json -Depth 10) -UseBasicParsing
        
        $bookingId = ($response.Content | ConvertFrom-Json).id
        Write-Host "? Booking created: $bookingId" -ForegroundColor Green
        
        # Step 4: Assign driver to booking
        Write-Host "`nStep 4: Assigning driver to booking..." -ForegroundColor Yellow
        
        $assignmentBody = @{
            driverId = $driverId
        }
        
        $response = Invoke-WebRequest -Method POST -Uri "$AdminApiUrl/bookings/$bookingId/assign-driver" `
            -Headers $headers -Body ($assignmentBody | ConvertTo-Json) -UseBasicParsing
        
        $assignment = $response.Content | ConvertFrom-Json
        
        if ($assignment.assignedDriverUid) {
            Write-Host "? Driver assigned successfully" -ForegroundColor Green
            Write-Host "  Driver ID: $($assignment.assignedDriverId)" -ForegroundColor Gray
            Write-Host "  Driver Name: $($assignment.assignedDriverName)" -ForegroundColor Gray
            Write-Host "  Driver UID: $($assignment.assignedDriverUid)" -ForegroundColor Gray
        } else {
            Write-Host "? Driver assignment failed" -ForegroundColor Red
            return $false
        }
        
        # Step 5: Verify booking shows driver assignment
        Write-Host "`nStep 5: Verifying booking details..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method GET -Uri "$AdminApiUrl/bookings/$bookingId" `
            -Headers $headers -UseBasicParsing
        
        $booking = $response.Content | ConvertFrom-Json
        
        if ($booking.assignedDriverUid -eq "test-driver-charlie-001") {
            Write-Host "? Booking correctly shows driver assignment" -ForegroundColor Green
            Write-Host "  Assigned Driver: $($booking.assignedDriverName)" -ForegroundColor Gray
            Write-Host "  Driver UID: $($booking.assignedDriverUid)" -ForegroundColor Gray
            Write-Host "  Status: $($booking.status)" -ForegroundColor Gray
        } else {
            Write-Host "? Driver assignment not reflected in booking" -ForegroundColor Red
            return $false
        }
        
        # Step 6: Test driver lookup by UserUid
        Write-Host "`nStep 6: Testing driver lookup..." -ForegroundColor Yellow
        
        $response = Invoke-WebRequest -Method GET -Uri "$AdminApiUrl/drivers/by-uid/test-driver-charlie-001" `
            -Headers $headers -UseBasicParsing
        
        $foundDriver = $response.Content | ConvertFrom-Json
        
        if ($foundDriver.id -eq $driverId) {
            Write-Host "? Driver lookup by UserUid successful" -ForegroundColor Green
            Write-Host "  Found driver: $($foundDriver.name)" -ForegroundColor Gray
        } else {
            Write-Host "? Driver lookup failed" -ForegroundColor Red
            return $false
        }
        
        Write-Host "`n? DRIVER ASSIGNMENT TEST PASSED" -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "? DRIVER ASSIGNMENT TEST FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
        return $false
    }
}

# Run test if called directly
if ($AdminToken) {
    $result = Test-DriverAssignment
    if (-not $result) {
        exit 1
    }
} else {
    Write-Host "Error: AdminToken parameter required" -ForegroundColor Red
    exit 1
}
