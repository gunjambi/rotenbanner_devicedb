# Device Model to Display Name Database (C#)

A library for converting Apple and Android device models into their human readable names:

```C#
using Rotenbanner;

DeviceNameLookup.TryGetDisplayName("iPhone10,6"); // "iPhone X"
DeviceNameLookup.TryGetDisplayName("GT-S6810B"); // "Samsung Galaxy Fame"
DeviceNameLookup.TryGetDisplayName("iPhone10,7"); // null, no such device
```

The conversion is performed using the embedded database. No network requests are made.

## License

The source code in this repository is licensed under MIT License.

I believe the database itself is a collection of facts, and hence not copyrightable.

## Versioning

This package is updated automatically every 2 weeks and a new version will be published with a version `1.0.YEAR|MONTH|DAY`.
For example, version 1.0.20260105 would be a database built on 2026-01-05.
See the repository's Github Action for the implementation.

## Alternatives to Consider

https://github.com/NaverPayDev/device-info, Typescript. Android and iOS devices.

https://github.com/zfdang/android-device-model-to-marketing-name, Android flavored Java, only Android devices.

https://github.com/jaredrummler/AndroidDeviceNames, Android flavored Java, only Android devices.

https://github.com/souzainf3/RNDeviceName, Swift, Apple devices only.

https://github.com/kimdaehee0824/DeviceNameKit, Swift, Apple devices only. Does some ~~insane~~ intriguing network requests.

https://www.npmjs.com/package/ios-device-list, Javascript, iOS only.

## Updating Database

To update the DB manually, see [Generator Readme](Generator/README.md)

## Q & A:

**Can I use this on Unity:**

I guess? I guess you could call `DeviceNameLookup.TryGetDisplayName(SystemInfo.deviceModel)` but I haven't tested if this works. Specifically I haven't tested that a) Embedded Resources work b) BrotliStream works on Unity.
Removing BrotliStream is easy if need be and replacing Embedded Resource with a file seems easy too.

In short, Unity support is not in the scope of this library but it might incidentally work.

**I checked the source and what the heck bro, what's this Trie stuff:**

This library was initally intended to be a pure ~~javascript~~ EMCAScript library.
Since embedding in JS means having a huge string literal of base64 or a huge integer table, I really wanted to squeeze the database where possible.

Since names have a lot of redundancy, especially prefix redundancy, a trie compresses them well.

**Okay, sure, the filesize explains it... Except I could just take the CSV, zip it and it would be smaller than the trie mess:**

Yep. Turns out that lookup structure from the model name to the display-name-trie-node takes quite a bit of space. Sure, we could have computed it at load time... but I had this misconception that I could simply mmap the resource and minimize unevictable memory usage.

It could be made better, but realistically, who cares of a few hundred kilobytes.

**So you want me to include your opaque binary blob, with poor justification:**

Yuppers.
I really recommend checking the [Generator Readme](Generator/README.md) and build your own database.
I promise the included database doesn't contain anything weird but in the long term, it's cheaper to verify than to trust.
Also, by generating the DB yourself you get the latest devices there.

**What's, with, these, commas:**

Sty,le.
