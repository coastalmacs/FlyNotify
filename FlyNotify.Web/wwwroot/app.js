document.addEventListener('DOMContentLoaded', () => {
    // DOM Elements
    const form = document.getElementById('add-profile-form');
    const departureInput = document.getElementById('departure');
    const arrivalInput = document.getElementById('arrival');
    const travelDateInput = document.getElementById('travel-date');
    const travelEndDateInput = document.getElementById('travel-end-date');
    const passengersInput = document.getElementById('passengers');
    
    const btnLiveScrape = document.getElementById('btn-live-scrape');
    const btnMockScrape = document.getElementById('btn-mock-scrape');
    const btnClearLogs = document.getElementById('btn-clear-logs');
    
    const profilesTbody = document.getElementById('profiles-tbody');
    const profileCountBadge = document.getElementById('profile-count');
    const nextRunText = document.getElementById('next-run-text');
    const logConsole = document.getElementById('log-console');

    // Date defaults: start tomorrow, end tomorrow
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const tomorrowStr = tomorrow.toISOString().split('T')[0];
    
    travelDateInput.value = tomorrowStr;
    travelDateInput.min = new Date().toISOString().split('T')[0]; // Can't select past dates
    travelEndDateInput.value = tomorrowStr;
    travelEndDateInput.min = tomorrowStr;

    // Date synchronization logic (similar to WPF validation requirements)
    travelDateInput.addEventListener('change', () => {
        travelEndDateInput.min = travelDateInput.value;
        if (travelEndDateInput.value < travelDateInput.value) {
            travelEndDateInput.value = travelDateInput.value;
        }
    });

    // Fetch and render profiles
    async function fetchProfiles() {
        try {
            const response = await fetch('/api/profiles');
            if (!response.ok) throw new Error('Failed to fetch profiles');
            const profiles = await response.json();
            
            profileCountBadge.textContent = `${profiles.length} Active`;
            
            if (profiles.length === 0) {
                profilesTbody.innerHTML = `
                    <tr>
                        <td colspan="10" class="empty-state">No flight tracking profiles configured. Add a profile above.</td>
                    </tr>
                `;
                return;
            }

            profilesTbody.innerHTML = profiles.map(p => {
                const statusClass = getStatusClass(p.availabilityStatus);
                const displayStatus = p.availabilityStatus || 'TBD';
                
                return `
                    <tr>
                        <td class="date-cell">${escapeHtml(p.fullScheduleDisplay)}</td>
                        <td><code>${escapeHtml(p.flightNumber)}</code></td>
                        <td>${escapeHtml(p.departureAirport)}</td>
                        <td>${escapeHtml(p.arrivalAirport)}</td>
                        <td>${escapeHtml(p.duration || 'TBD')}</td>
                        <td><span class="badge-cabin">${escapeHtml(p.cabinClass)}</span></td>
                        <td>${p.passengerCount}</td>
                        <td class="status-cell" title="${escapeHtml(p.detailedStatus || '')}">
                            <span class="${statusClass}">${escapeHtml(displayStatus)}</span>
                        </td>
                        <td>${escapeHtml(p.lastCheckedDisplay)}</td>
                        <td>
                            <a href="${p.qantasQueryUrl}" target="_blank" class="action-link">Qantas</a>
                            <span class="divider">|</span>
                            <a href="${p.expertFlyerQueryUrl}" target="_blank" class="action-link">ExpertFlyer</a>
                            <span class="divider">|</span>
                            <a href="#" class="action-link link-delete" data-dept="${p.departureAirport}" data-arr="${p.arrivalAirport}" data-date="${p.travelDate.split('T')[0]}" data-flight="${p.flightNumber}">Delete</a>
                        </td>
                    </tr>
                `;
            }).join('');

            // Hook up delete links
            document.querySelectorAll('.link-delete').forEach(link => {
                link.addEventListener('click', async (e) => {
                    e.preventDefault();
                    const dept = e.target.getAttribute('data-dept');
                    const arr = e.target.getAttribute('data-arr');
                    const date = e.target.getAttribute('data-date');
                    const flight = e.target.getAttribute('data-flight');
                    await deleteProfile(dept, arr, date, flight);
                });
            });

        } catch (error) {
            console.error('Error loading profiles:', error);
        }
    }

    // Fetch and render logs
    async function fetchLogs() {
        try {
            const response = await fetch('/api/logs');
            if (!response.ok) throw new Error('Failed to fetch logs');
            const logs = await response.json();
            
            if (logs.length === 0) {
                logConsole.innerHTML = '<div class="log-line system-line">[System] No event logs yet.</div>';
                return;
            }

            const currentScrollPos = logConsole.scrollTop;
            const isAtBottom = logConsole.scrollHeight - logConsole.clientHeight <= logConsole.scrollTop + 10;

            logConsole.innerHTML = logs.map(line => {
                let cssClass = 'log-line';
                if (line.includes('[System]') || line.includes('Service Started')) {
                    cssClass += ' system-line';
                }
                return `<div class="${cssClass}">${escapeHtml(line)}</div>`;
            }).join('');

            // Keep scrolled to bottom if was already at bottom
            if (isAtBottom) {
                logConsole.scrollTop = logConsole.scrollHeight;
            }

            // Estimate next schedule run based on logs
            updateNextRunDisplay(logs);

        } catch (error) {
            console.error('Error loading logs:', error);
        }
    }

    // Submitting a new profile
    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        
        // Get checked cabins
        const checkedCabins = Array.from(document.querySelectorAll('input[name="cabins"]:checked'))
            .map(cb => cb.value)
            .join(',');

        if (!checkedCabins) {
            alert('Please select at least one Cabin Class.');
            return;
        }

        const newProfile = {
            departureAirport: departureInput.value.toUpperCase().trim(),
            arrivalAirport: arrivalInput.value.toUpperCase().trim(),
            travelDate: travelDateInput.value + 'T00:00:00Z',
            travelEndDate: travelEndDateInput.value + 'T00:00:00Z',
            passengerCount: parseInt(passengersInput.value, 10),
            cabinClass: checkedCabins,
            flightNumber: "TBD",
            availabilityStatus: "TBD",
            detailedStatus: "TBD"
        };

        try {
            const response = await fetch('/api/profiles', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(newProfile)
            });

            if (response.ok) {
                // Refresh table
                fetchProfiles();
                // Add a local visual notification
                appendLocalLog(`[System] Added flight profile: ${newProfile.departureAirport} → ${newProfile.arrivalAirport}`);
            } else {
                const errText = await response.text();
                alert('Failed to add profile: ' + errText);
            }
        } catch (error) {
            alert('Error adding profile: ' + error.message);
        }
    });

    // Deleting a profile
    async function deleteProfile(dept, arr, date, flight) {
        try {
            const url = `/api/profiles?departure=${encodeURIComponent(dept)}&arrival=${encodeURIComponent(arr)}&travelDate=${encodeURIComponent(date)}&flightNumber=${encodeURIComponent(flight)}`;
            const response = await fetch(url, { method: 'DELETE' });
            
            if (response.ok) {
                fetchProfiles();
                appendLocalLog(`[System] Removed flight profile: ${dept} → ${arr} on ${date}`);
            } else {
                alert('Failed to delete profile.');
            }
        } catch (error) {
            alert('Error deleting profile: ' + error.message);
        }
    }

    // Trigger Scan
    async function triggerScan(isLive) {
        const type = isLive ? 'live' : 'test';
        appendLocalLog(`[System] Requesting immediate background ${type} scan...`);
        
        const btn = isLive ? btnLiveScrape : btnMockScrape;
        btn.disabled = true;
        
        try {
            const response = await fetch(`/api/scrape-now?isLive=${isLive}`, { method: 'POST' });
            if (response.ok) {
                appendLocalLog(`[System] Background ${type} scan successfully scheduled! Watch the logs below.`);
            } else {
                appendLocalLog(`[System] Error scheduling scan.`);
            }
        } catch (error) {
            appendLocalLog(`[System] Error triggering scan: ${error.message}`);
        }
        
        setTimeout(() => { btn.disabled = false; }, 3000);
    }

    btnLiveScrape.addEventListener('click', () => triggerScan(true));
    btnMockScrape.addEventListener('click', () => triggerScan(false));

    btnClearLogs.addEventListener('click', async () => {
        try {
            await fetch('/api/logs', { method: 'DELETE' });
        } catch (e) {
            // ignore
        }
        logConsole.innerHTML = '<div class="log-line system-line">[System] Console cleared.</div>';
    });

    // Helper functions
    function appendLocalLog(text) {
        const line = `[${new Date().toLocaleTimeString()}] ${text}`;
        logConsole.innerHTML += `<div class="log-line system-line">${escapeHtml(line)}</div>`;
        logConsole.scrollTop = logConsole.scrollHeight;
    }

    function formatDates(start, end) {
        if (!start) return '';
        const s = start.split('T')[0];
        const e = end ? end.split('T')[0] : s;
        return s === e ? s : `${s} to ${e}`;
    }

    function translateCabins(cabinsStr) {
        if (!cabinsStr) return 'F';
        // Map fare bucket code letters back to readable representation
        let out = [];
        if (cabinsStr.includes('Y')) out.push('Y');
        if (cabinsStr.includes('W')) out.push('W');
        if (cabinsStr.includes('J')) out.push('J');
        if (cabinsStr.includes('F')) out.push('F');
        return out.join('/') || cabinsStr;
    }

    function getStatusClass(status) {
        if (!status) return 'status-tbd';
        const s = status.toUpperCase();
        if (s.includes('AVAILABLE')) return 'status-available';
        if (s.includes('CHECKED')) return 'status-checked';
        return 'status-tbd';
    }

    function formatLastChecked(dateStr) {
        if (!dateStr || dateStr.startsWith('0001-01-01') || dateStr.startsWith('1970-01-01')) {
            return 'Never';
        }
        const d = new Date(dateStr);
        return d.toLocaleString();
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;')
                  .replace(/</g, '&lt;')
                  .replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;')
                  .replace(/'/g, '&#039;');
    }

    function truncateString(str, num) {
        if (str.length <= num) return str;
        return str.slice(0, num) + '...';
    }

    function updateNextRunDisplay(logs) {
        // Scan logs backward for scheduling lines
        for (let i = logs.length - 1; i >= 0; i--) {
            const line = logs[i];
            if (line.includes('Next automated scan scheduled for')) {
                const match = line.match(/scheduled for (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})/);
                if (match && match[1]) {
                    const nextRunDateStr = match[1];
                    const nextRunTime = new Date(nextRunDateStr.replace(' ', 'T'));
                    if (!isNaN(nextRunTime)) {
                        const now = new Date();
                        const diffMs = nextRunTime - now;
                        if (diffMs > 0) {
                            const totalMins = Math.floor(diffMs / (1000 * 60));
                            const hrs = Math.floor(totalMins / 60);
                            const mins = totalMins % 60;
                            nextRunText.textContent = `${nextRunDateStr} (In ${hrs}h ${mins}m)`;
                            return;
                        }
                    }
                    nextRunText.textContent = nextRunDateStr;
                    return;
                }
            }
        }
        nextRunText.textContent = "Scheduled to run daily at 10:00 AM";
    }

    // Initial load and polling intervals
    fetchProfiles();
    fetchLogs();
    
    setInterval(fetchProfiles, 5000);
    setInterval(fetchLogs, 5000);
});
