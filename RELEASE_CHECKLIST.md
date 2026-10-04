# EmilyDesk release checklist

- [ ] Run `Build.cmd` on Windows and confirm it reports success.
- [ ] Install `Output\EmilyDeskInstaller.exe` on a test Windows desktop and open the dashboard, Designer, main widgets, and Ember Glow optional widgets.
- [ ] Check that `Output\OptionalWidgets` contains the optional `.emilywidget` packages.
- [ ] Read `BuildLogs\installer-validation.txt` and record the installer SHA-256 and signing status.
- [ ] Create a GitHub Release and attach the completed installer. Add optional widget packages as separate assets if offering direct downloads.
- [ ] Check the README artwork, MIT license, Sponsor link, and repository social preview on GitHub.
