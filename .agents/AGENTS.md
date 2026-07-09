# FlyNotify Workspace Rules

These rules define behavior constraints, architectural requirements, and business logic guidelines for FlyNotify workspace components. All agent interactions, refactorings, or modifications must adhere to them.

## Wildcard and Region Query Preservation

* **Definition**: A flight profile query is classified as a wildcard or region query if its `ArrivalAirport` is set to `"ALL"` or matches any compact two-letter region code in the `TravelRegion` enum (e.g. `NZ`, `US`, `UK`, `EU`, `SE`, `NA`, `ME`, `WN`, `CN`, `EN`, `LA`, `AF`).
* **Preservation Constraint**: Wildcard and region profiles must **never** be overwritten with specific flight details (such as specific flight numbers, specific airport codes, or schedules) when matching flights are found. They must remain in the monitored collection list as persistent query matrices.
* **Separation of Results**: When a wildcard or regional check yields flight matches, each match must be created or updated as a **new, separate specific flight profile** (e.g. `ArrivalAirport = "ZQN"`, `FlightNumber = "QF123"`) in the list rather than overwriting the wildcard query profile.
