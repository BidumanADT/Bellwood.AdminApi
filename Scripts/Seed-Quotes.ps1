#Requires -Version 5.1
<#
.SYNOPSIS
    Seeds test quote data to the Bellwood AdminAPI with Phase Alpha statuses.

.DESCRIPTION
    Creates sample quotes covering all Phase Alpha statuses for manual UI testing:
    - Pending: New quotes awaiting dispatcher acknowledgment
    - Acknowledged: Dispatcher acknowledged, preparing response
    - Responded: Price/ETA sent to passenger
    - Accepted: Passenger accepted, booking created
    - Cancelled: Quote cancelled

.PARAMETER ApiBaseUrl
    The base URL of the AdminAPI. Default: https://localhost:5206

.PARAMETER AuthServerUrl
    The base URL of the AuthServer. Default: https://localhost:5001

.EXAMPLE
    .\Seed-Quotes.ps1
    
.EXAMPLE
    .\Seed-Quotes.ps1 -ApiBaseUrl "https://localhost:5206" -AuthServerUrl "https://localhost:5001"
#>

param(
    [string]$ApiBaseUrl = "https://localhost:5206",
    [string]$AuthServerUrl = "https://localhost:5001"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Bellwood - Seed Phase Alpha Quotes" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# For PowerShell 5.1 - ignore certificate validation
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

# Step 1: Get JWT Tokens for different users
Write-Host "Step 1: Authenticating users..." -ForegroundColor Yellow

# Admin token for seeding
try {
    $adminLogin = @{
        username = "alice"
        password = "password"
    } | ConvertTo-Json

    $adminResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $adminLogin `
        -UseBasicParsing

    $adminToken = $adminResponse.accessToken
    $adminHeaders = @{ "Authorization" = "Bearer $adminToken" }
    Write-Host "? Admin authenticated successfully" -ForegroundColor Green
}
catch {
    Write-Host "? Admin authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Make sure AuthServer is running on $AuthServerUrl" -ForegroundColor Yellow
    exit 1
}

# Dispatcher token for lifecycle actions
try {
    $dianaLogin = @{
        username = "diana"
        password = "password"
    } | ConvertTo-Json

    $dianaResponse = Invoke-RestMethod -Uri "$AuthServerUrl/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $dianaLogin `
        -UseBasicParsing

    $dianaToken = $dianaResponse.accessToken
    $dianaHeaders = @{ "Authorization" = "Bearer $dianaToken" }
    Write-Host "? Dispatcher (Diana) authenticated successfully" -ForegroundColor Green
}
catch {
    Write-Host "? Dispatcher authentication failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Create quotes with different statuses
Write-Host "Step 2: Creating Phase Alpha test quotes..." -ForegroundColor Yellow
Write-Host ""

$createdQuotes = @()
$now = Get-Date

# === QUOTE 1: PENDING (for manual testing of acknowledge flow) ===
Write-Host "Creating PENDING quote (awaiting acknowledgment)..." -ForegroundColor Cyan
$pendingQuote = @{
    booker = @{
        firstName = "John"
        lastName = "Smith"
        phoneNumber = "312-555-1001"
        emailAddress = "john.smith@example.com"
    }
    passenger = @{
        firstName = "Jane"
        lastName = "Smith"
        phoneNumber = "312-555-1002"
        emailAddress = "jane.smith@example.com"
    }
    vehicleClass = "Sedan"
    pickupDateTime = $now.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "O'Hare International Airport"
    pickupStyle = "Curbside"
    dropoffLocation = "Downtown Chicago, 100 N LaSalle St"
    roundTrip = $false
    passengerCount = 2
    checkedBags = 2
    carryOnBags = 1
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
        -Method POST `
        -Headers $adminHeaders `
        -Body $pendingQuote `
        -ContentType "application/json" `
        -UseBasicParsing
    
    $createdQuotes += @{ Id = $response.id; Status = "Pending" }
    Write-Host "  ? Created: $($response.id)" -ForegroundColor Green
}
catch {
    Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# === QUOTE 2: ACKNOWLEDGED (for manual testing of respond flow) ===
Write-Host "Creating ACKNOWLEDGED quote (ready for price response)..." -ForegroundColor Cyan
$acknowledgedQuote = @{
    booker = @{
        firstName = "Sarah"
        lastName = "Johnson"
        phoneNumber = "847-555-2001"
        emailAddress = "sarah.johnson@example.com"
    }
    passenger = @{
        firstName = "Sarah"
        lastName = "Johnson"
        phoneNumber = "847-555-2001"
        emailAddress = "sarah.johnson@example.com"
    }
    vehicleClass = "SUV"
    pickupDateTime = $now.AddDays(4).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "Midway Airport, Terminal 1"
    pickupStyle = "Curbside"
    dropoffLocation = "Oak Park, 150 N Oak Park Ave"
    roundTrip = $false
    passengerCount = 4
    checkedBags = 4
    carryOnBags = 2
} | ConvertTo-Json

try {
    # Create quote
    $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
        -Method POST `
        -Headers $adminHeaders `
        -Body $acknowledgedQuote `
        -ContentType "application/json" `
        -UseBasicParsing
    
    $quoteId = $response.id
    
    # Acknowledge it
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/acknowledge" `
        -Method POST `
        -Headers $dianaHeaders `
        -UseBasicParsing | Out-Null
    
    $createdQuotes += @{ Id = $quoteId; Status = "Acknowledged" }
    Write-Host "  ? Created & acknowledged: $quoteId" -ForegroundColor Green
}
catch {
    Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# === QUOTE 3: RESPONDED (for manual testing of passenger acceptance) ===
Write-Host "Creating RESPONDED quote (awaiting passenger acceptance)..." -ForegroundColor Cyan
$respondedQuote = @{
    booker = @{
        firstName = "Michael"
        lastName = "Chen"
        phoneNumber = "773-555-3001"
        emailAddress = "michael.chen@company.com"
    }
    passenger = @{
        firstName = "Michael"
        lastName = "Chen"
        phoneNumber = "773-555-3001"
        emailAddress = "michael.chen@company.com"
    }
    vehicleClass = "Sedan"
    pickupDateTime = $now.AddDays(5).AddHours(10).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "Union Station, 210 S Canal St"
    pickupStyle = "MeetAndGreet"
    dropoffLocation = "O'Hare International Airport, Terminal 5"
    roundTrip = $false
    passengerCount = 1
    checkedBags = 2
    carryOnBags = 1
} | ConvertTo-Json

try {
    # Create quote
    $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
        -Method POST `
        -Headers $adminHeaders `
        -Body $respondedQuote `
        -ContentType "application/json" `
        -UseBasicParsing
    
    $quoteId = $response.id
    
    # Acknowledge it
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/acknowledge" `
        -Method POST `
        -Headers $dianaHeaders `
        -UseBasicParsing | Out-Null
    
    # Respond with price/ETA
    $quoteResponse = @{
        estimatedPrice = 85.50
        estimatedPickupTime = $now.AddDays(5).AddHours(9).AddMinutes(45).ToString("yyyy-MM-ddTHH:mm:ss")
        notes = "Estimated based on standard route pricing. Final price subject to confirmation."
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/respond" `
        -Method POST `
        -Headers $dianaHeaders `
        -Body $quoteResponse `
        -ContentType "application/json" `
        -UseBasicParsing | Out-Null
    
    $createdQuotes += @{ Id = $quoteId; Status = "Responded" }
    Write-Host "  ? Created, acknowledged & responded: $quoteId" -ForegroundColor Green
    Write-Host "    Price: `$85.50, Pickup: 15 min early" -ForegroundColor Gray
}
catch {
    Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# === QUOTE 4: ACCEPTED (for viewing accepted quote panel) ===
Write-Host "Creating ACCEPTED quote (converted to booking)..." -ForegroundColor Cyan
$acceptedQuote = @{
    booker = @{
        firstName = "Lisa"
        lastName = "Martinez"
        phoneNumber = "312-555-4001"
        emailAddress = "lisa.martinez@example.com"
    }
    passenger = @{
        firstName = "Lisa"
        lastName = "Martinez"
        phoneNumber = "312-555-4001"
        emailAddress = "lisa.martinez@example.com"
    }
    vehicleClass = "S-Class"
    pickupDateTime = $now.AddDays(6).AddHours(18).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "Navy Pier, 600 E Grand Ave"
    pickupStyle = "Curbside"
    dropoffLocation = "Midway Airport, Terminal 2"
    roundTrip = $false
    passengerCount = 2
    checkedBags = 3
    carryOnBags = 1
} | ConvertTo-Json

try {
    # Create quote
    $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
        -Method POST `
        -Headers $adminHeaders `
        -Body $acceptedQuote `
        -ContentType "application/json" `
        -UseBasicParsing
    
    $quoteId = $response.id
    
    # Acknowledge
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/acknowledge" `
        -Method POST `
        -Headers $dianaHeaders `
        -UseBasicParsing | Out-Null
    
    # Respond with price/ETA
    $quoteResponse = @{
        estimatedPrice = 125.00
        estimatedPickupTime = $now.AddDays(6).AddHours(17).AddMinutes(45).ToString("yyyy-MM-ddTHH:mm:ss")
        notes = "Premium vehicle requested. VIP service confirmed."
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/respond" `
        -Method POST `
        -Headers $dianaHeaders `
        -Body $quoteResponse `
        -ContentType "application/json" `
        -UseBasicParsing | Out-Null
    
    # Accept (creates booking)
    $acceptResponse = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/accept" `
        -Method POST `
        -Headers $adminHeaders `
        -UseBasicParsing
    
    $createdQuotes += @{ Id = $quoteId; Status = "Accepted"; BookingId = $acceptResponse.bookingId }
    Write-Host "  ? Created, acknowledged, responded & accepted: $quoteId" -ForegroundColor Green
    Write-Host "    Booking created: $($acceptResponse.bookingId)" -ForegroundColor Gray
}
catch {
    Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# === QUOTE 5: CANCELLED (for viewing cancelled quote panel) ===
Write-Host "Creating CANCELLED quote..." -ForegroundColor Cyan
$cancelledQuote = @{
    booker = @{
        firstName = "Robert"
        lastName = "Taylor"
        phoneNumber = "847-555-5001"
        emailAddress = "robert.taylor@example.com"
    }
    passenger = @{
        firstName = "Robert"
        lastName = "Taylor"
        phoneNumber = "847-555-5001"
        emailAddress = "robert.taylor@example.com"
    }
    vehicleClass = "Sedan"
    pickupDateTime = $now.AddDays(7).AddHours(15).ToString("yyyy-MM-ddTHH:mm:ss")
    pickupLocation = "Willis Tower, 233 S Wacker Dr"
    pickupStyle = "Curbside"
    dropoffLocation = "Union Station, 210 S Canal St"
    roundTrip = $false
    passengerCount = 1
    checkedBags = 1
    carryOnBags = 0
} | ConvertTo-Json

try {
    # Create quote
    $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
        -Method POST `
        -Headers $adminHeaders `
        -Body $cancelledQuote `
        -ContentType "application/json" `
        -UseBasicParsing
    
    $quoteId = $response.id
    
    # Cancel it immediately
    Invoke-RestMethod -Uri "$ApiBaseUrl/quotes/$quoteId/cancel" `
        -Method POST `
        -Headers $adminHeaders `
        -UseBasicParsing | Out-Null
    
    $createdQuotes += @{ Id = $quoteId; Status = "Cancelled" }
    Write-Host "  ? Created & cancelled: $quoteId" -ForegroundColor Green
}
catch {
    Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# === ADD 2 MORE PENDING QUOTES for badge count testing ===
Write-Host "Creating additional PENDING quotes (for badge count testing)..." -ForegroundColor Cyan

for ($i = 6; $i -le 7; $i++) {
    $additionalQuote = @{
        booker = @{
            firstName = "Test"
            lastName = "User$i"
            phoneNumber = "312-555-${i}001"
            emailAddress = "testuser$i@example.com"
        }
        passenger = @{
            firstName = "Test"
            lastName = "Passenger$i"
            phoneNumber = "312-555-${i}002"
            emailAddress = "passenger$i@example.com"
        }
        vehicleClass = "Sedan"
        pickupDateTime = $now.AddDays($i + 2).ToString("yyyy-MM-ddTHH:mm:ss")
        pickupLocation = "Test Location $i"
        pickupStyle = "Curbside"
        dropoffLocation = "Test Destination $i"
        roundTrip = $false
        passengerCount = 1
        checkedBags = 1
        carryOnBags = 0
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$ApiBaseUrl/quotes" `
            -Method POST `
            -Headers $adminHeaders `
            -Body $additionalQuote `
            -ContentType "application/json" `
            -UseBasicParsing
        
        $createdQuotes += @{ Id = $response.id; Status = "Pending" }
        Write-Host "  ? Created: $($response.id)" -ForegroundColor Green
    }
    catch {
        Write-Host "  ? Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Seeding Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Created quotes summary:" -ForegroundColor White
Write-Host ""

# Count by status
$statusCounts = $createdQuotes | Group-Object -Property Status | ForEach-Object {
    @{ Status = $_.Name; Count = $_.Count }
}

foreach ($statusCount in $statusCounts) {
    $color = switch ($statusCount.Status) {
        "Pending" { "Yellow" }
        "Acknowledged" { "Cyan" }
        "Responded" { "Magenta" }
        "Accepted" { "Green" }
        "Cancelled" { "Gray" }
        default { "White" }
    }
    Write-Host "  $($statusCount.Status): $($statusCount.Count)" -ForegroundColor $color
}

Write-Host ""
Write-Host "Test scenarios ready:" -ForegroundColor Cyan
Write-Host "  1. Navigate to quotes page and verify badge shows pending count (3)" -ForegroundColor Gray
Write-Host "  2. Test acknowledge flow on a Pending quote" -ForegroundColor Gray
Write-Host "  3. Test respond flow on an Acknowledged quote" -ForegroundColor Gray
Write-Host "  4. View Responded quote (passenger perspective)" -ForegroundColor Gray
Write-Host "  5. View Accepted quote with booking link" -ForegroundColor Gray
Write-Host "  6. View Cancelled quote panel" -ForegroundColor Gray
Write-Host ""

exit 0
