# iot-edge-filewatcher

Azure IoT Edge module which monitors file creation and puts the filename as messages on the IoT Edge routing 

This module is forked from [here](https://github.com/iot-edge-foundation/iot-edge-filewatcher), customized it for only filename monitoring.

## Container create options

This module makes use of Docker volumes to detect and access files on disk.

Use these container create options:

```
{
  "HostConfig": {
    "Binds": [
      "[Host folder]:/app/exchange"
    ]
  }
}
```

An example is:

```
{
  "HostConfig": {
    "Binds": [
      "/var/iot-edge-filewatcher/exchange:/app/exchange"
    ]
  }
}
``` 

The module will automatically create the 'exchange' folder. Please give the folder elevated rights to allow renaming files after processing. 

This can be done with:

```
sudo chmod 666 exchange
```

## Module twin desired properties

The module check the 'exchange' folder every few seconds to detect new files using a certain search pattern.

After processing, the file is renamed using a new extension which is appended to the orginal filename.

With desired properties, we can change the behavior:

* interval, the interval in milliseconds - Default 10000
* searchPattern, the pattern mapped on filenames  - Default '*.txt'
* renameExtension, the appended extension to prevent a file being read twice - Default '.old'

## Messages

The messages outputted are JSON messages that contains the filename (full path) and filesize (byte) of the incoming file.

```
06/25/2021 03:53:13 - Seen 0 files with pattern '*.txt'
06/25/2021 03:53:23 - Seen 0 files with pattern '*.txt'
06/25/2021 03:53:33 - Seen 1 files with pattern '*.txt'
File found: '/app/exchange/a.txt' - Size: 15 bytes.
Sending message: {"filename":"/app/exchange/a.txt","filesize":15}
Renamed '/app/exchange/a.txt' to '/app/exchange/a.txt.old'
06/25/2021 03:53:44 - Seen 0 files with pattern '*.txt'
06/25/2021 03:53:54 - Seen 0 files with pattern '*.txt'
06/25/2021 03:54:04 - Seen 0 files with pattern '*.txt'
```

## File access

Keep in mind the module needs access to the folder. Please check the console log output of the module to see if right are elevated correctly. 

A simple check for access to the file is built in.

## File location

This module is tested with files available on the local file system only.

## Contributions

Sourcecode is available at [GitHub](https://github.com/iot-edge-foundation/iot-edge-filewatcher).

An example container is available on [Docker Hub](https://hub.docker.com/repository/docker/svelde/iot-edge-filewatcher).

Our project accepts GitHub pull requests :-) 
