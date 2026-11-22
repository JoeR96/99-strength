# Constraints and Risks: A2S Workout Tracking Application

## Executive Summary

This document identifies technical constraints, potential issues, edge cases, and risks for the A2S Workout Tracking Application. Understanding these early allows for proactive design decisions.

---

## 1. Technical Constraints

### 1.1 Platform Constraints

#### Mobile Device Limitations
| Constraint | Impact | Mitigation |
|------------|--------|------------|
| Limited memory | Large aggregate loading issues | Lazy loading, pagination |
| Battery consumption | Background sync drains battery | Batch sync, minimize polling |
| Storage quotas | iOS limits IndexedDB to 50MB | Efficient schema, data archival |
| Offline periods | Extended gym sessions without signal | Local-first architecture |
| Screen interruptions | Phone calls during workout | Auto-save on every action |
| App backgrounding | iOS kills background apps | Persist state immediately |

#### Platform-Specific Issues
- **iOS**: Aggressive app termination, limited background tasks
- **Android**: Battery optimization may kill background processes
- **Web (PWA)**: Limited storage, no push notifications on iOS

### 1.2 Data Constraints

#### Volume Estimates (Per Program)
| Data Type | Quantity | Estimated Size |
|-----------|----------|----------------|
| Weeks | 21 | - |
| Workout Days | ~84 (4 days/week) | - |
| Exercise Plans | ~420 (5 exercises/day) | ~50 KB |
| Sets Recorded | ~2,100 (5 sets/exercise) | ~100 KB |
| TM Records | ~20 exercises x 21 updates | ~20 KB |
| Total per program | - | ~200-500 KB |

#### Historical Data
- Multiple programs over years: 2-5 MB typical
- Acceptable for local storage
- Consider archival for very old programs

### 1.3 Performance Constraints

| Operation | Target Latency | Critical Path |
|-----------|----------------|---------------|
| Open workout | < 200ms | Yes |
| Record set | < 50ms | Yes |
| Calculate TM | < 10ms | Yes |
| Load analytics | < 500ms | No |
| Full sync | < 5s | No |

### 1.4 Connectivity Constraints

- Gym environments often have poor signal
- WiFi may be unavailable or unreliable
- Must work 100% offline for workout recording
- Sync can be deferred indefinitely

---

## 2. Concurrency and Consistency Risks

### 2.1 Multi-Device Scenario

**Risk**: User has app on phone and tablet, edits on both.

**Scenarios:**
1. Start workout on phone, continue on tablet
2. Adjust TM manually on tablet while workout in progress on phone
3. Both devices complete workouts for same day

**Mitigation Strategies:**
- **v1**: Single-device assumption, warn if conflict detected
- **v2**: Implement last-write-wins with conflict UI
- **v3**: CRDT for automatic merge (complex)

### 2.2 Week Transition Race Condition

**Risk**: Workout completion triggers week advance while another operation in progress.

**Scenario:**
```
1. User completes Day 4 workout (final workout of week)
2. System starts week completion process
3. User quickly opens "View Week" on another screen
4. Race between week update and view
```

**Mitigation:**
- Optimistic locking on week state
- Queue week transitions, process sequentially
- Clear UI state on week change

### 2.3 TM Update Atomicity

**Risk**: TM update partially applied.

**Scenario:**
```
1. Workout completes with 3 AMRAP results
2. First TM update succeeds
3. Second TM update fails (device crash)
4. Inconsistent state: some TMs updated, others not
```

**Mitigation:**
- All TM updates in single transaction
- Store pending updates until confirmed
- Recovery process to complete pending updates

---

## 3. Week Transition Edge Cases

### 3.1 Skipped Week

**Scenario**: User misses entire training week.

**Options:**
1. **Strict**: Cannot skip weeks, must complete or mark each workout
2. **Lenient**: Allow marking entire week as skipped
3. **Auto-advance**: After N days, auto-advance with warning

**Recommendation**: Option 2 with confirmation dialog.

**TM Impact**: No change (skipped workouts don't affect TM).

### 3.2 Partial Week Completion

**Scenario**: User completes 2 of 4 workouts, wants to advance.

**Options:**
1. Block advancement until all workouts addressed
2. Allow advancement with warning
3. Auto-mark remaining as skipped

**Recommendation**: Require explicit action on each workout (complete or skip).

### 3.3 Retroactive Workout Entry

**Scenario**: User forgot to log workout, enters it 3 days later.

**Considerations:**
- Date assignment (when scheduled vs when entered)
- TM calculation timing
- Week progression impact

**Recommendation:**
- Allow backdated entry
- Apply TM adjustment immediately
- Future weeks use updated TM

### 3.4 Block Transition

**Scenario**: Completing week 7 (end of Block 1).

**Special Handling:**
- Mark block as complete
- Next week uses Block 2 parameters
- Rep schemes change significantly
- Visual indication of new block

### 3.5 Deload Scheduling Conflict

**Scenario**: User wants to move deload week.

**Options:**
1. Fixed deload at weeks 7/14/21 (strict)
2. Allow deload shifting within block
3. Skip deload entirely (advanced users)

**Recommendation**: Allow configuration, default to strict.

---

## 4. Data Migration Risks

### 4.1 Schema Evolution

**Risk**: App updates require database schema changes.

**Scenarios:**
- Adding new columns to existing tables
- Restructuring relationships
- Renaming fields

**Mitigation:**
- Versioned migrations
- Always additive changes when possible
- Data transformation scripts
- Rollback capability

### 4.2 Import from Spreadsheet

**Risk**: Users have existing A2S data in Excel/Google Sheets.

**Considerations:**
- Various spreadsheet formats exist
- User customizations to spreadsheets
- Mapping spreadsheet columns to app schema

**Recommendation:**
- Support official SBS spreadsheet format
- Validate imported data
- Allow manual correction during import

### 4.3 Export for Analysis

**Risk**: Users want data in other formats.

**Required Exports:**
- CSV for spreadsheet analysis
- JSON for developers/API
- PDF for printing workout logs

---

## 5. Calculation Risks

### 5.1 Floating Point Precision

**Risk**: TM calculations produce imprecise values.

**Example:**
```
TM = 100 kg
Adjustment = +1.5%
Calculated = 100 * 1.015 = 101.50000000000001
```

**Mitigation:**
- Round to appropriate precision after each calculation
- Use integer cents/grams internally, display in kg/lbs
- Consistent rounding (round half up)

### 5.2 Cumulative Rounding Errors

**Risk**: Many small adjustments compound errors.

**Example:**
```
Week 1: 100 -> 101.5 (rounds to 102)
Week 2: 102 -> 103.5 (rounds to 104)
Week 3: 104 -> 105.6 (rounds to 106)
vs
Week 1: 100.0 -> 101.5 -> 103.02 -> 104.56 (rounds to 105)
```

**Mitigation:**
- Store TM with higher precision than display
- Round only for display and barbell loading
- Document rounding approach

### 5.3 Extreme Values

**Risk**: TM becomes unreasonably high or low.

**Scenarios:**
- User massively outperforms (+10 reps on AMRAP)
- User consistently fails (multiple -5% adjustments)
- Manual entry errors (1000 kg squat)

**Mitigation:**
- Cap single-session adjustment at +/-10%
- Warn on TM outside expected range
- Validate manual entries

### 5.4 Division by Zero / Invalid States

**Risk**: Edge cases in formulas.

**Scenarios:**
- Zero rep AMRAP (immediate failure)
- Zero TM (should never happen)
- Negative reps (invalid input)

**Mitigation:**
- Validate all inputs before calculation
- Handle edge cases explicitly
- Never allow TM = 0

---

## 6. User Experience Risks

### 6.1 Data Loss Fear

**Risk**: Users afraid app will lose their workout data.

**Symptoms:**
- Reluctance to use app
- Manual backup to spreadsheet
- Low trust

**Mitigation:**
- Save every action immediately
- Clear sync status indicators
- Easy data export
- Visible backup confirmation

### 6.2 Complex Workout Entry

**Risk**: Recording sets takes too long between exercises.

**Impact:**
- Extends rest periods
- Frustration during workout
- Abandonment mid-workout

**Mitigation:**
- One-tap set completion for standard sets
- Pre-fill expected values
- Large touch targets
- Minimal required fields

### 6.3 Overwhelming Configuration

**Risk**: Too many options for new users.

**Mitigation:**
- Smart defaults
- Progressive disclosure
- "Quick Start" option
- Advanced settings hidden initially

### 6.4 Confusion Between Variants

**Risk**: User doesn't understand RTF vs RIR vs Linear.

**Mitigation:**
- Clear explanations during setup
- Visual differentiation
- Cannot change variant mid-program
- Recommend based on experience level

---

## 7. Edge Cases Catalog

### 7.1 Workout Recording Edge Cases

| Edge Case | Handling |
|-----------|----------|
| 0 reps on AMRAP | Valid, apply -5% TM |
| >30 reps on AMRAP | Cap at +5 bonus for TM calc |
| Negative reps entered | Reject input |
| Decimal reps entered | Round to integer |
| Recording same set twice | Overwrite previous |
| Recording set for completed workout | Warn, allow |
| Recording set for future workout | Warn, allow |

### 7.2 TM Edge Cases

| Edge Case | Handling |
|-----------|----------|
| TM would become 0 or negative | Clamp to minimum (e.g., 20 kg) |
| TM increase > 10% single session | Cap at 10% |
| TM decrease > 20% single session | Allow (user may be injured) |
| TM for new exercise | Require setup before use |
| TM unit mismatch (kg/lbs) | Convert on entry |

### 7.3 Program Edge Cases

| Edge Case | Handling |
|-----------|----------|
| Start week 1 twice | Block, program already started |
| Jump from week 5 to week 10 | Block, must progress sequentially |
| Two active programs | Block, one at a time |
| Program duration > 21 weeks | Not applicable, fixed structure |
| No workouts in a day | Allow empty days |
| 10+ exercises in one day | Allow but warn about volume |

### 7.4 Time-Related Edge Cases

| Edge Case | Handling |
|-----------|----------|
| Workout spans midnight | Use start time for date |
| Timezone change mid-program | Keep original timezone |
| Clock manipulation | Detect, warn user |
| Very long workout (>4 hours) | Allow, suggest session split |
| Very short workout (<5 minutes) | Allow, might be makeup |

---

## 8. Security Considerations

### 8.1 Data Privacy

| Concern | Mitigation |
|---------|------------|
| Workout data is personal | Encrypt at rest |
| Weight data is sensitive | User-controlled sharing |
| No PII in analytics | Anonymize before export |

### 8.2 Data Integrity

| Concern | Mitigation |
|---------|------------|
| Tampering with history | Checksums on records |
| Fake PRs | No leaderboards, personal use only |
| Corrupted database | Regular integrity checks |

### 8.3 Authentication (If Cloud Sync)

| Concern | Mitigation |
|---------|------------|
| Unauthorized access | Standard auth (OAuth/JWT) |
| Session hijacking | Short-lived tokens |
| Password requirements | Follow OWASP guidelines |

---

## 9. Operational Risks

### 9.1 App Store Approval

**Risk**: Rejection for various reasons.

**Mitigation:**
- Follow platform guidelines
- No prohibited content
- Proper privacy policy
- Health data disclaimers

### 9.2 Backend Availability (If Cloud)

**Risk**: Server downtime affects users.

**Mitigation:**
- Local-first architecture
- Graceful degradation
- Clear offline indicators
- Queue operations for retry

### 9.3 Third-Party Dependencies

**Risk**: Library updates break functionality.

**Mitigation:**
- Lock dependency versions
- Automated testing on updates
- Minimal dependencies
- Vendor critical libraries

---

## 10. Risk Priority Matrix

| Risk | Likelihood | Impact | Priority | Mitigation Status |
|------|------------|--------|----------|-------------------|
| Data loss during workout | Medium | High | P1 | Auto-save design |
| Offline not working | Medium | High | P1 | Local-first arch |
| TM calculation errors | Low | High | P1 | Thorough testing |
| Multi-device conflicts | Medium | Medium | P2 | v1: single device |
| Week transition race | Low | Medium | P2 | Sequential processing |
| Complex UI overwhelms | Medium | Medium | P2 | Progressive disclosure |
| Floating point errors | Low | Low | P3 | Rounding strategy |
| Schema migration issues | Low | Medium | P2 | Versioned migrations |
| Very long programs | Low | Low | P3 | Fixed 21 weeks |
| Extreme TM values | Low | Low | P3 | Validation bounds |

---

## 11. Assumptions and Dependencies

### Assumptions
1. User has modern smartphone (iOS 14+ / Android 10+)
2. User understands basic workout terminology
3. User knows their approximate 1RM or recent performance
4. Single user per device/account
5. English language only for v1

### Dependencies
| Dependency | Type | Risk if Unavailable |
|------------|------|---------------------|
| React Native | Framework | Core app broken |
| SQLite | Database | No data storage |
| Device Storage | Hardware | App unusable |
| Timer APIs | Platform | Rest timer broken |
| App Stores | Distribution | Can't distribute |

---

## 12. Contingency Plans

### If Offline Sync Fails
- Provide manual export (JSON/CSV)
- Allow fresh start with TM import
- Cloud backup as separate feature

### If Performance Issues
- Profile and optimize hot paths
- Consider native modules for calculations
- Lazy load non-critical features

### If User Adoption Low
- Gather feedback on friction points
- Simplify onboarding
- Consider guided tutorial
