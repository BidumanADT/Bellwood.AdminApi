Alpha Test Readiness & Final Preparation Plan for Bellwood App
Current Readiness Summary (Phase 2 state)
•	Features delivered:
•	Passenger quoting & booking: The mobile app allows passengers to request quotes and create bookings. Pickup/drop‑off selection uses Google Places autocomplete and persists coordinates; results are stored in a JSON file for later reference[1].
•	Driver real‑time tracking: Drivers can log in and expose their location; dispatchers see real‑time driver positions.
•	Secure login & role‑based access: AuthServer issues JWTs with role claims. Phase 2 introduced a new dispatcher role in addition to admin, booker (passenger) and driver. The AdminAPI enforces role-based policies such as AdminOnly and StaffOnly. Dispatchers can view quotes and bookings but cannot see billing data due to field‑masking helpers[2].
•	Data isolation and auditing: Each resource (quote, booking, driver) records CreatedByUserId and ModifiedByUserId, enabling per‑user filtering and an audit trail[3]. Helpers such as MaskBillingFields hide payment methods and totals for dispatchers[4].
•	AdminPortal: Provides dispatchers with list views of quotes and bookings; data is masked for sensitive fields. Admin users can manage credentials and assign roles[5].
•	Security: The AdminAPI stores OAuth credentials securely and masks secrets; tokens use JWT with no refresh flow[6].
•	Tests: All Phase 2 RBAC and masking tests pass (10/10)[7].
•	Storage: All components still use JSON file‑based storage (no EF Core). Ownership metadata (CreatedByUserId) ensures each user only sees his or her own records.
•	Limo Anywhere integration: Phase 2 introduced a stub service (LimoAnywhereServiceStub). It contains methods to get customer and operator details, import ride history and sync bookings, but each method logs a TODO and returns default values[8]. A TestConnection endpoint returns success if OAuth credentials exist but does not call LimoAnywhere[9]. Full integration is deferred to Phase 3.
•	Limitations & gaps:
•	Quote lifecycle management: Passengers can submit quotes, and dispatchers see them, but there is no way for dispatchers to acknowledge receipt or send price/ETA responses. The AdminPortal doesn’t allow editing quote status or sending messages, and passengers cannot see updates.
•	No refresh tokens: JWTs must be re‑issued manually; this is acceptable for an alpha test but should be addressed later.
•	JSON storage concurrency: File‑based storage may cause conflicts under concurrent use; for alpha testing this is acceptable but a database should be adopted in later phases.
•	Limo Anywhere integration: No live price estimation or booking sync exists; testers will need placeholder logic for quotes.
Alpha Test Objectives
1.	Enable end‑to‑end quote lifecycle management using placeholder logic (until Limo Anywhere is integrated) so that passengers can see a response to their quote requests and convert accepted quotes to bookings.
2.	Ensure all user roles—passenger, driver, dispatcher, admin—can complete their typical workflows without encountering unimplemented features or unhandled errors.
3.	Validate role-based data isolation and auditing across the apps under realistic usage scenarios.
4.	Prepare smoke tests and UI behaviors so testers know exactly what to expect and can provide feedback.
5.	Keep code changes minimal and self‑contained: avoid database migrations or large architecture changes; rely on existing JSON storage and stub services.
Roadmap & Deliverables by Component
1. AdminAPI (backend)
The AdminAPI controls core business logic and enforces RBAC. To support alpha testing, we need to extend the Quote model and add endpoints for the quote lifecycle while maintaining JSON storage.
1.1. Data model changes
•	Extend the Quote entity to include:
•	Status (string): values such as "pending", "acknowledged", "responded", "accepted", "cancelled". Default is pending.
•	AcknowledgedAt (DateTime?) and AcknowledgedByUserId to record when a dispatcher acknowledges a quote.
•	RespondedAt (DateTime?) and RespondedByUserId.
•	EstimatedPrice (decimal?) and EstimatedPickupTime (DateTime?) – placeholder fields where dispatchers enter price and pickup time.
•	Notes (string?) for any additional comments.
•	These fields should be persisted in the existing JSON file with the same ownership metadata (CreatedByUserId, ModifiedByUserId) and included in the audit trail.
•	No database migration: simply adjust the data contract and update JSON serialization.
1.2. Endpoints (additions)
Endpoint	Method	Description	Authorization
/quotes	GET	List quotes. Supports optional query parameters status=... and createdBy=.... Dispatchers see all quotes, bookers see only their quotes.	AdminOnly and StaffOnly for full list; BookerOnly for own quotes
/quotes/{id}	GET	Retrieve a quote by ID. Applies field masking for dispatchers (hide payment details)[4].
Owner or staff
/quotes/{id}/acknowledge	POST	Dispatcher acknowledges receipt of a quote. Sets Status to acknowledged, populates AcknowledgedAt and AcknowledgedByUserId.	StaffOnly (dispatchers or admins)
/quotes/{id}/respond	POST	Dispatcher submits an estimated price and pickup time; sets status to responded, populates response fields, records RespondedByUserId. Payload contains estimatedPrice, estimatedPickupTime, optional notes. Returns updated quote.	StaffOnly
/quotes/{id}/accept	POST	Booker accepts the quote and converts it to a booking. Creates a new Booking (with Status = "pending") and sets the quote to accepted.	BookerOnly
/quotes/{id}/cancel	POST	Booker or staff cancels the quote. Sets status to cancelled.	Owner or staff
Implementation notes: - Use existing helper methods to enforce RBAC; add a new policy StaffOnly that includes both admin and dispatcher roles (already defined)[2]. - Mask payment/billing fields for dispatchers using the existing MaskBillingFields helper[4]. - For price estimation, implement a placeholder algorithm that approximates cost based on distance (e.g., $2 per mile + base fee), or accept manual entry from the dispatcher. Store values in the JSON but mark them as placeholder in the UI.
1.3. LimoAnywhere stub utilization
•	Continue using LimoAnywhereServiceStub with no real API calls; the only functional method remains TestConnection[8].
•	For now, ignore methods like GetCustomer or ImportRideHistory until Phase 3.
1.4. Testing & validation
•	Update integration tests to cover new endpoints: verifying that only staff can acknowledge/respond, bookers can accept their own quotes, and auditors see correct history.
•	Add negative tests to ensure unauthorized access returns 403.
•	Expand smoke-test script to hit new endpoints and verify JSON updates.
2. AdminPortal (dispatcher/admin web app)
The AdminPortal is the primary interface for dispatchers. To support alpha testing, it needs UI enhancements for quote lifecycle and management.
2.1. Quote management dashboard
•	Quote list view: Add filters for status (e.g., Pending, Acknowledged, Responded, Accepted, Cancelled). Display columns: requestor name, pickup/drop‑off summary, requested date, status, created time.
•	Quote detail view: When clicking a quote, show all details, including masked billing fields for dispatchers[2]. Provide actions depending on status:
•	Pending: Show Acknowledge button.
•	Acknowledged: Provide fields to enter EstimatedPrice and EstimatedPickupTime plus optional notes; clicking Send Response calls the /respond endpoint.
•	Responded: Display the dispatched price/time. Read-only; waiting for passenger acceptance.
•	Accepted or Cancelled: Read-only view; optionally show link to associated booking.
2.2. Booking management
•	For quotes marked Accepted, automatically create bookings via AdminAPI’s existing booking endpoint. Provide a link to navigate to the booking’s detail page.
•	Continue to display driver assignments and real‑time tracking for bookings.
•	Add an indicator that the booking originated from a quote (use a Source = QuoteId field).
2.3. Usability enhancements
•	Show toast notifications or list badges when new quotes arrive (via WebSocket or periodic polling).
•	Provide a placeholder section explaining that price estimates are approximate and will be replaced by live Limo Anywhere rates in a future release.
•	Keep existing RBAC enforcement: dispatchers can’t see credit-card details; admin can. Provide tooltips for masked fields to explain the policy.
3. Passenger App (mobile)
Phase 2 provided quote submission and booking creation but no way to view responses. For alpha testing, the mobile app needs to surface the quote status and allow acceptance or cancellation.
3.1. Quote tracking & notifications
•	My Quotes screen: Add a new page accessible from the home menu showing the passenger’s quote requests. Show status and summary info: destination, pickup time, status, estimated price (if responded).
•	When a dispatcher acknowledges or responds, show a notification (push notification if supported or an in-app banner) indicating the new status.
•	Quote detail view: Display the full quote along with any response (estimated price/ETA). Provide actions:
•	Responded status: show Accept and Cancel buttons. Accepting calls the /quotes/{id}/accept endpoint; canceling calls /cancel.
•	Pending or Acknowledged: read-only; inform the user that the dispatcher will respond soon.
3.2. Booking from quote
•	Upon acceptance, navigate to the existing bookings page (BookRidePage). Prefill pickup/drop‑off data and price from the quote.
•	Provide an informational banner that the booking was generated from a quote.
•	Keep payment processing as currently implemented (credit-card details collected if needed).
3.3. UX notes
•	Because storage is still local JSON, implement periodic polling for quote status (e.g., every 30 seconds) until websockets are adopted.
•	Clearly label price estimates as approximate and subject to change.
•	For alpha testers, include a “Report Issue” button to capture bug or feedback.
4. Driver App
Drivers are mainly impacted by bookings, not quotes. Current features include real‑time location tracking and viewing assigned bookings. For alpha testing, ensure drivers can:
•	View bookings assigned to them when a quote is accepted and converted to a booking.
•	See pickup/drop‑off details and passenger contact info (masked for privacy where appropriate).
•	Mark a booking as started, in progress and completed.
No major changes are required; ensure RBAC allows only drivers to see their assignments and update their status.
5. AuthServer (authentication & role management)
•	Verify that the new endpoints are protected using existing policies. No new roles are required beyond dispatcher which is already defined in Phase 2.
•	Expose an admin‑only endpoint in AuthServer to list users with their roles. This will help testers confirm role assignments.
•	Consider adding a low‑impact refresh-token mechanism in a later phase; it is not essential for alpha but will improve user experience.
Placeholder Logic for Quote Estimation
Until Limo Anywhere integration is enabled, dispatchers need a simple method to estimate price and pickup time. Here are two options:
1.	Manual entry: Provide a text input for price and a date/time picker for pickup. Dispatchers can compute a price using internal rate tables and manual reasoning. This approach keeps the system flexible and emphasises that these are manual estimates.
2.	Heuristic algorithm: Implement a basic function in AdminAPI that calculates an estimate based on distance and service type (e.g., base fare $10 + $2 per mile). This algorithm can run when the dispatcher clicks “Generate Estimate,” after which they can adjust the price before sending.
In either case, record in the quote that the estimate is a placeholder. When Phase 3 integrates Limo Anywhere, these fields can be replaced with actual quotes from the external API.
Smoke Test & Quality Assurance Plan
To ensure readiness for alpha testing, execute the following test suite:
1.	Authentication and role checks: Verify that admin, dispatcher, booker, and driver roles can log in and access only permitted endpoints. Ensure masked fields behave correctly[2].
2.	Quote lifecycle: Submit quote requests from the passenger app; verify they appear in the AdminPortal. Acknowledge and respond via AdminPortal; confirm that the passenger sees status changes and can accept or cancel.
3.	Booking creation from quote: After acceptance, check that a new booking is created with appropriate links to the quote and is visible to the driver.
4.	Driver workflows: Drivers should see new bookings, update their status, and track trip progress.
5.	Security & auditing: Confirm that CreatedByUserId and ModifiedByUserId are populated on quotes and bookings; ensure unauthorized access attempts return 403.
6.	LimoAnywhere stub: Call the TestConnection endpoint with and without stored OAuth credentials to verify expected responses (200 or 503)[9].
7.	Performance & concurrency: Simulate multiple passengers submitting quotes simultaneously to test JSON storage concurrency. Monitor for race conditions and file locks; adjust code to handle simple file locks (e.g., using asynchronous write queues) if necessary.
Final Considerations & Future Phases
•	Documentation updates: Update Phase2-Summary.md and User Access Control documents to include the new quote statuses and endpoints so the entire team has a single source of truth.
•	Database migration: For Phase 3 or later, plan the transition from JSON storage to a relational database via EF Core. This will support concurrent writes and simplify query logic.
•	Limo Anywhere integration: When ready, implement OAuth 2.0 flows and replace placeholder estimation with calls to Limo Anywhere’s quoting API. The stub’s method definitions are already in place[8].
•	Refresh token flow: Implement refresh tokens in AuthServer to improve user session longevity.
•	Monitoring & analytics: Add logging and telemetry to gather performance data during alpha testing; this will inform scaling decisions.
Summary
The Bellwood project has completed Phase 2 and is largely production ready—RBAC, secure login, real‑time driver tracking, and user data isolation are in place[6][4]. However, to conduct a meaningful alpha test without Limo Anywhere integration, we must close the quote‑lifecycle gap. The plan above introduces lightweight data‑model extensions, new API endpoints, and UI enhancements across the AdminAPI, AdminPortal, and Passenger App to enable dispatchers to acknowledge quotes, send price/ETA responses, and allow passengers to accept or cancel quotes. These changes are self‑contained, rely on existing JSON storage, and include placeholder logic for price estimation. With this roadmap, testers can exercise all primary user flows and provide feedback ahead of Phase 3 integration.

