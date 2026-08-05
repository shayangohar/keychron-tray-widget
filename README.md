# Keychron K8 HE Battery Tray

This project provides a small Windows tray app for the Keychron K8 HE.

The app reads the battery report from the keyboard. It works in Cable mode
and through the Keychron Link 2.4 GHz receiver. It does not use Bluetooth or
the Keychron application.

## Features

- Show the battery percentage when you point to the tray icon.
- Show the current transport: Wired, 2.4 GHz, or Bluetooth.
- Show the voltage and charge state when the firmware provides them.
- Show the active HE analog profile as `HE profile N/3`.
- Start with Windows.
- Use a read-only HID request.

## Requirements

- Windows x64.
- .NET 10 Desktop Runtime.
- A K8 HE with the wired-aware firmware from this repository.
- The Keychron Link receiver for 2.4 GHz readings.

The app does not need a Bluetooth connection. The firmware reports the
transport and charge state. Cable mode reports the battery value over the
direct USB HID endpoint.

## Build

Run these commands from the repository root:

```powershell
dotnet build src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release
dotnet run --project src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -- --self-test
dotnet publish src/KeychronK8BatteryTray/KeychronK8BatteryTray.csproj -c Release -r win-x64 --self-contained false -o publish
```

The `publish` folder contains the app and `hidapi.dll`. Keep these files in
the same folder.

Run the app from a stable folder, such as:

```text
%LocalAppData%\KeychronK8Battery
```

Right-click the tray icon and select **Start with Windows**.

The app writes one value under the current user's Windows Run key. It does not
need administrator access.

## Check the HID connection

Run the one-shot probe:

```powershell
publish/KeychronK8BatteryTray.exe --probe
```

Example output:

```text
WiredPresent=True; Battery=100; VoltageMillivolts=4172; Charging=Charging; Transport=Usb; AnalogProfile=2/3
```

The app uses these device values:

| Device | VID | PID | Usage page | Usage |
| --- | ---: | ---: | ---: | ---: |
| K8 HE over USB | `3434` | `0E80` | `FF60` | `61` |
| Keychron Link receiver | `3434` | `D030` | `FF60` | `61` |

The app checks the direct USB endpoint first. If it does not return a report,
the app checks the receiver.

## HID commands

The wired-aware firmware accepts a 33-byte HID report. The first byte is the
report ID used by HIDAPI.

### Battery: `0xA4`

Request:

```text
00 A4 00 00 ... 00
```

Response:

```text
A4 PP VL VH CS TR MI 00 ... 00
```

The response can have a leading zero report ID. The fields are:

| Field | Meaning |
| --- | --- |
| `PP` | Battery percentage, from 0 to 100. |
| `VL VH` | Battery voltage in millivolts, little-endian. |
| `CS` | `0` not charging, `1` charging, `2` full. |
| `TR` | `1` USB, `2` Bluetooth, `4` 2.4 GHz. |
| `MI` | Model ID. `2` is the K8 HE. |

The request is read-only. It does not change charging or the wireless mode.

### HE profile: `0xA9`

Request:

```text
00 A9 10 00 ... 00
```

Response:

```text
A9 10 PI PC 00 ... 00
```

`PI` is the zero-based active profile index. `PC` is the profile count. The
K8 HE reports three profiles. The app shows the index as `PI + 1`, for
example `HE profile 2/3`.

## Tray states

| State | Tooltip example |
| --- | --- |
| Wired and charging | `K8 HE: 100% - Wired - 4172 mV - Charging - HE profile 2/3` |
| Wired and full | `K8 HE: 100% - Wired - 4172 mV - Full - HE profile 2/3` |
| 2.4 GHz | `K8 HE: 91% - 2.4 GHz - 4020 mV - HE profile 1/3` |
| Not detected | `K8 HE: Not detected - last seen 91% - HE profile 1/3` |

The app checks the keyboard once per minute. Moving the pointer over the icon
can request an earlier check. Use **Refresh now** for an immediate check.

## Firmware files

For this app, use the wired-aware files in
[`firmware/k8_he_battery_rawhid_wired/`](firmware/k8_he_battery_rawhid_wired/):

| Layout | Firmware file |
| --- | --- |
| ANSI / US | [`k8_he_ansi_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_ansi_battery_rawhid_wired.bin) |
| ISO | [`k8_he_iso_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_iso_battery_rawhid_wired.bin) |
| JIS | [`k8_he_jis_battery_rawhid_wired.bin`](firmware/k8_he_battery_rawhid_wired/k8_he_jis_battery_rawhid_wired.bin) |

Use the file that matches the physical keyboard layout. Do not flash a file
for another layout. The `.bin` file is the recommended file for QMK Toolbox.

The folder also contains the matching `.hex` files, SHA-256 checksums, the
source patch, and the firmware README.

The older firmware files in `firmware/` report only the battery percentage.
They do not provide the wired-mode fields used by the current app.

## Recovery

If the custom firmware causes a problem, flash the matching official file from
[`rollback/`](rollback/) with QMK Toolbox.

Use Cable mode and a direct USB connection for recovery. Do not update the
receiver for this operation.

## Third-party software

The app includes HIDAPI. Its license is in
[`src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt`](src/KeychronK8BatteryTray/native/HIDAPI-LICENSE.txt).
