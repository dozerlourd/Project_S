# Headless PlayMode Test Runner

Use `Scripts/Run-HeadlessPlayModeTests.ps1` for sequential Unity Headless PlayMode validation.

```powershell
./Scripts/Run-HeadlessPlayModeTests.ps1 -TestFilter @(
  'ProjectS.Tests.PlayMode.UnitTacticalBehaviorPlayModeTests',
  'ProjectS.Tests.PlayMode.FogOfWarPlayModeTests'
)
```

The runner invokes Unity in the current PowerShell process, waits for each test filter to finish, and reads only that run's unique XML under `TestResults/Headless`. It refuses to run while another Unity process is active. Do not use the shared `LocalLow/.../TestResults.xml` file for verification decisions.
