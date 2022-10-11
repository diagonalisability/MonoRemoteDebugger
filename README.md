MonoRemoteDebugger for FNA MonoKickstart
============

This fork is for running [FNA MonoKickstart](https://github.com/flibitijibibo/MonoKickstart) programs. It was forked from [Techl's repo](https://github.com/techl/MonoRemoteDebugger).

Usage for automatically uploading files to the target computer
---
Download a release from this Github repo or build the Visual Studio extension yourself.

On the target computer, unzip `Server.zip` and run the server with `mono`
```
$ unzip Server.zip
$ cd Server
$ mono MonoRemoteDebugger.Server.exe
```

On the debugging computer
- Install the VS extension (the .vsix file)
- Open the VS project you want to debug
- Set some breakpoints
- Press `Extensions -> MonoRemoteDebugger -> Debug with Mono (remote)`
- Ensure "Upload binaries to debugging server" is checked
- Enter the ip address of the target computer
- Press "Connect"

The program should run on the target computer until it hits a breakpoint.

Usage for manually starting the MonoKickstart program on the remote computer
---
On the target computer, run the MonoKickstart program with this environment variable:
```
MONO_BUNDLED_OPTIONS='--debugger-agent=address=0.0.0.0:11000,transport=dt_socket,server=y --debug=mdb-optimizations' ./kick.bin.x86_64
```
On the debugging computer
- Follow the steps above, except...
- Ensure "Upload binaries to debugging server" is unchecked

Known issues
---
- If you try pressing "Connect" the first time you open Visual Studio after installing it, VS will crash. I'm not sure if this is a VS bug or a bug in MonoRemoteDebugger. To fix this you can either let VS crash and restart itself, or open a project for the first time after installing this extension, let it load, then close it and re-open it.

Also, the README from Techl's repo listed these issues:
- Breakpoints on user threads are not supported
- Visual Basic and F# are not supported
- Unstable on .Net Core Common Project System
