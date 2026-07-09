# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-09

### Added
- **IsWildcardOrRegion Property**: Added a property on `FlightProfile` to distinguish between query templates (wildcard `"ALL"` or `TravelRegion` enum values) and specific flight listings.
- **Dynamic Region Resolver**: Extracted airport metadata from Qantas props to dynamically map individual airport destinations (e.g., `ZQN`, `CHC`, `AKL`) to region codes (e.g., `NZ`).
- **DataGrid Column Sorting**: Added sorting event handling (`FlightDataGrid_Sorting`) for column headers, allowing chronological sorting for Date and Last Checked columns.
- **Workspace Agent Rules**: Created `.agents/AGENTS.md` to persist behavior constraints and region-splitting rules for AI assistants on other development machines.

### Changed
- **Astro Scraper Migration**: Replaced Next.js payload streaming regex parsing with Astro island React properties decoding using `HtmlAgilityPack` and `System.Text.Json`.
- **Puppeteer Load Strategy**: Updated page navigation wait conditions to `Networkidle2` to ensure complete loading of Astro components.

### Fixed
- **Query Overwriting**: Fixed regional queries (e.g., `SYD -> NZ`) getting overwritten and destroyed when specific flight matches were found.
- **Result Splitting**: Ensured multiple matches under a wildcard or region query are successfully created as separate, specific flight profiles.
- **Spam Prevention**: Implemented internal try-catch blocks to abort manual and automated batch runs immediately upon encountering the first error (e.g., rate limits or Cloudflare blocks).
