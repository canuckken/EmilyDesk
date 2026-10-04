XWidget Reborn 2.6.7 adds Dashboard Engine online/offline controls.

XWidget Reborn 2.6.7
====================

XWidget Reborn is now an independent desktop widget platform. The original
XWidget application is no longer the architectural foundation of the project.

Phase 2 introduces a dedicated runtime:

    RebornEngine.exe

The runtime owns native widget lifecycle and windows. Support for existing
XWidget widgets is provided through a separate Legacy XWidget compatibility
adapter. During the migration period, the original host may still run as a
fallback for legacy features the new adapter does not yet implement.

See ARCHITECTURE.md for the dependency rules and migration policy.


2.6.5 keeps RebornEngine.exe alive even when every widget is closed. See ENGINE_ZERO_WIDGET_TEST_2.6.5.md.
