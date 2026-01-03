# Database generator

Generator generates `Database.dat` file from the XCode database and Google Play certified devices.

Usage:
```
Generator -GooglePlayDevices supported_devices.csv -AppleDevices device_traits.db
```

Place the generated `Database.dat` into `Lookup/Resources/Database.dat`

## Google Play Device Database

Google Play provides a list of certified android devices. To fetch `supported_devices.csv`, download https://storage.googleapis.com/play_public/supported_devices.csv

For more information, see https://storage.googleapis.com/play_public/supported_devices.html

## XCode Device Database

XCode includes a simple database which includes the device model.

* Download XCode
* Extract the XIP with https://github.com/bitcoin-core/apple-sdk-tools . This results in a Portable SUSv2 CPIO archive.
* From the CPIO archive, extract `./Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/usr/standalone/device_traits.db`

