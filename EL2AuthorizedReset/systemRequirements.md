EL2 Authorized Reset
=========

Validation flow
---------

This should all be triggered when an associate badge swipes for machine reset

- Verify that badge number is registered in the associate info table (*badge number as PK*)
- Upon finding badge number in DB, lookup line name from CMMS number and associate number from badge number
- Check that the authorizing line name is in the list of line names available to the associate
- If successful, inform the system to proceed, otherwise inform the system to reject
- Regardless, document reset attempt with details in historical

Admin interfaces
---------

Access should be restricted by some other authentication (i.e. Windows login, need IT help)

- **CSV upload:** Assuming no live lookup, update the registry of CMMS numbers to locations
- **Associate Management:** Add/remove associates from DB
- **Line Management:** Add/remove lines by associate

Multiplicities
---------

- Badge to associate: `1..1` (Every associate has a badge)
- Associate to line `1..n` (Every associate has at least one line, otherwise they shouldn't be in the system)
- CMMS to line `1..1` (Every CMMS number uniquely identifies a line name)

Tables
---------

- Associate info (badge number to associate number and admin privileges)
- Associate number to line names
- CMMS number to line name
- Historical (timestamp, associate number, CMMS number, line name, and whether authorized)

No foreign keying for historical, and ideally some way of connecting locks to their unlocks, be able to trace long locks and potential machine operating around lock.
