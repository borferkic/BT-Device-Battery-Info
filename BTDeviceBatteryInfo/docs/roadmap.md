# Roadmap

## Current state

The project is published as **BT Device Battery Info** and has a functional Windows foundation: connected-device discovery, PnP/GATT battery reading where available, automatic Bose/QuietComfort prioritization, system tray support, internal preferences, and local logging.

## Next steps

- Add unit tests for `ReconnectPolicy`, settings, and data transformations.
- Define a validation matrix by device model, driver, and Windows version.
- Prepare a distributable package with an icon, visible version, and release notes.
- Evaluate an update and uninstall approach that does not depend solely on manual publishing.
- Review accessibility, localization, and error states in the UI.

## Current scope boundary

The application cannot generally command Bluetooth Classic audio-profile connections through a public desktop API. Any improvement in this area must rely on an official, verifiable Windows capability; destructive commands and simulated states must not be introduced.
