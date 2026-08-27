# XPScript services

XPScript can compile a `.xps` source file as a long-running operating-system service. The same service source model is used on Windows, Linux and macOS.

## Service source

A service file starts with `[Service]`. Service files contain procedures and declarations rather than ordinary top-level execution.

```xpscript
[Service]
[StopTimeout:30s]

Sub ServiceStart()
    Print "SERVICE-START"
End Sub

[Interval:30s]
Sub CheckIncomingFiles()
    Print "SERVICE-CHECK"
End Sub

[Interval:1h]
Sub Cleanup()
    Print "SERVICE-CLEANUP"
End Sub

Sub ServiceStop()
    Print "SERVICE-STOP"
End Sub
```

See [service-interval.xps](../samples/service-interval.xps) for a complete source file.

`ServiceStart()` and `ServiceStop()` are optional parameterless lifecycle hooks. `ServiceStart()` runs once when the service starts. `ServiceStop()` runs once after a graceful stop has stopped scheduling new work and all running interval jobs have completed within the stop timeout.

## Interval jobs

`[Interval:value]` applies to the parameterless Sub immediately following the rule. Supported units are seconds (`s`), minutes (`m`), hours (`h`) and days (`d`). Examples are `30s`, `4m`, `1h` and `1d`.

Each interval Sub is an independent scheduled job. A job runs once, returns, waits for its interval, and then runs again. The same job never overlaps with itself. Different interval jobs may execute independently.

An exception from one interval execution does not stop the scheduler. The failed execution ends and the job remains eligible for its next interval.

## Graceful stop

`[StopTimeout:value]` controls how long the runtime waits for active scheduled work during shutdown. The default is 30 seconds. It accepts the same `s`, `m`, `h` and `d` units as `[Interval]`.

When Windows Service Control Manager, systemd, launchd or a supported process shutdown signal requests stop, the runtime performs this sequence:

1. No new interval execution is started.
2. Running interval procedures are allowed to finish.
3. The runtime waits up to `StopTimeout` for running work.
4. `ServiceStop()` runs only after the active work has completed within the timeout.
5. The process exits.

A restart is owned by the operating-system service manager. It is a normal stop followed by a new process start, so the next process calls `ServiceStart()` again.

## Compile

Compile a service with the normal compiler command:

```text
xpscript compile service.xps
```

A `[Service]` file is not an ordinary `xpscript run` program. Compile it and install the compiled executable with the service command.

## Install

The initial install command is:

```text
xpscript service install <compiled-service> --name NAME --display-name "DISPLAY NAME" [--start auto|manual|disabled]
```

`--name` is the stable service identifier used by the native service manager. `--display-name` is the human-readable service name. Both are required. `--start` defaults to `manual`.

Startup modes are:

- `auto`: start automatically during system startup.
- `manual`: do not start automatically, but allow an administrator to start the service manually.
- `disabled`: do not start automatically and configure the native service manager so the service is disabled.

Before installation, XPScript checks the native service manager for the requested `--name`. Installation fails without replacing or modifying anything when that service name already exists.

The native mappings are Windows Service Control Manager on Windows, systemd on Linux and launchd on macOS. Installing system services normally requires administrator/root privileges.

Example:

```text
xpscript service install ./worker --name xps-worker --display-name "XPScript Worker" --start auto
```

The initial service-install surface intentionally does not include custom arguments, working-directory configuration or start-immediately behavior. Those can be added separately without changing the service source lifecycle.
