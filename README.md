# sshdeploy
 [![Analytics](https://ga-beacon.appspot.com/UA-8535255-2/unosquare/sshdeploy/)](https://github.com/igrigorik/ga-beacon)
[![Build Status](https://travis-ci.org/unosquare/sshdeploy.svg?branch=master)](https://travis-ci.org/unosquare/sshdeploy)
[![Build status](https://ci.appveyor.com/api/projects/status/p6c0whp2xfajuu0c?svg=true)](https://ci.appveyor.com/project/geoperez/sshdeploy)
[![NuGet version](https://badge.fury.io/nu/dotnet-sshdeploy.svg)](https://badge.fury.io/nu/dotnet-sshdeploy)

:star: *Please star this project if you find it useful!*

A `dotnet` CLI extension that enables quick deployments over SSH. This program was specifically designed to streamline .NET application development for the the Raspberry Pi running Raspbian. 

**If you came here looking for our old version of SSHDeploy please click [here](https://www.nuget.org/packages/SSHDeploy/), otherwise you are in the right place**

 The following commands are currently available:
 * `dotnet monitor` - Watches changes on a single file, if this event is raised then it proceeeds to send the specified source path files over SSH
 * `dotnet push` - Single use command that trasfers files over SSH


## Installation
As of now  CLI does not allow command line installation so you'll need to modify your csproj manually.

 ```xml
    <ItemGroup>
     <DotNetCliToolReference Include="dotnet-sshdeploy" Version="*" />
    </ItemGroup>
 ```
 
## Usage
 **There are two ways of passing arguments: the old school way using the cli and our approach using the csproj file.**
### Using the csproj file
#### Push
1. Edit your csproj file and add:
```xml
<PropertyGroup>
    <SshDeployHost>192.168.2.194</SshDeployHost>
    <SshDeployClean />
    <SshDeployTargetPath>/home/pi/libfprint-cs</SshDeployTargetPath>
    <SshDeployUsername>pi</SshDeployUsername>
    <SshDeployPassword>raspberry</SshDeployPassword>
    <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
</PropertyGroup>
 ``` 
2. We need a post build event as well:
 ```xml
<Target Condition="$(BuildingInsideSshDeploy) ==''" Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="cd $(ProjectDir)" />
    <Exec Command="dotnet sshdeploy push" />
</Target>
 ```
 *Voilà! sshdeploy  finds the necessary arguments provided using proper xml tags and deploys after a successful build*
 
 * **Be sure you are using ' */* ' with *RemoteTargetPath* otherwise it will not work.**
 * **You MUST use the property** `BuildingInsideSshDeploy` **to make sure this event will not be executed within sshdeploy's build method to avoid an infinite loop**
 * **If no RuntimeIdentifier is provided a [Framework-dependent deployment](https://docs.microsoft.com/en-us/dotnet/core/deploying/) will be created otherwise a [Self-contained deployment](https://docs.microsoft.com/en-us/dotnet/core/deploying/) will**
 #### Monitor
1. Go to your Visual Studio Solution (the one you intend to continously deploy to the Raspberry Pi).
2. Right click on the project and click on the menu item "Properties"
3. Go to the "Build Events" tab, and under Post-build events, enter the following: 
* `echo %DATE% %TIME% >> "$(TargetDir)sshdeploy.ready"`
	*This simply writes the date and time to the `sshdeploy.ready` file. Whenever this file CHANGES, the deployment tool will perform a deployment.
 4. Edit your csproj file and add:
```xml
    <RemoteHost>192.168.2.194</RemoteHost>
    <SourcePath>C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\bin\Debug</SourcePath>
    <RemoteTargetPath>/home/pi/libfprint-cs</RemoteTargetPath>
    <RemoteUsername>pi</RemoteUsername>
    <RemotePassword>raspberry</RemotePassword>
 ```
 5. Execute 
  ```
 dotnet sshdeploy monitor
 ```
 
 **FYI: Arguments passed using the csproj file will not override the ones provided using the cli**
 ### XML Tags
 Heres a complete list of arguments with their corresponding XML tag.
 
|      Args        |        XML Tag      | 
| :--------------: | :------------------:| 
| -m,--monitor     | `<SshDeployMonitorFile>`     |
| -f,--framework   |   `<TargetFramework>`        | 
| -r,--runtime     |   `<RuntimeIdentifier>`      | 
|  -s, --source    | `<SshDeploySourcePath>`      |
|  -t,--target     |`<SshDeployTargetPath>`       |
|  --pre           |  `<SshDeployPreCommand>`     | 
| --post           | `<SshDeployPostCommand>`     | 
|    --clean       | `<SshDeployClean/>`          |
|    --exclude     | `<SshDeployExclude>`         |
|  -v,--verbose    | `<SshDeployVerbose/>`        | 
|  -h,--host       |  `<SshDeployHost>`           |
| -p,--port        |   `<SshDeployPort>`          | 
|  -u,--username   |  `<SshDeployUsername>`       | 
|  -w,--password   |  `<SshDeployPassword>`       |
|  -l,--legacy     |     `<SshDeployLegacy/>`     |

### Old school way
#### Push
1. Navigate to  your project folder where the csproj file resides. Example:
```
cd C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\
```
2. Execute this command with some arguments. Here's a simple example:
```
dotnet sshdeploy push -f netcoreapp2.0 -t "/home/pi/libfprint-cs" -h 192.168.2.194
```
* In the command shown above :
	* `-f` refers to the source framework
	* `-t` refers to the target path 
	* `-h` refers to the host (IP address of the Raspberry Pi)
* For a detailed list of all the arguments available please see [below](#push-mode) or execute `dotnet sshdeploy push`

#### Monitor

The following steps outline a continuous deployment of a Visual Studio solution to a Raspberry Pi running the default Raspbian SSH daemon.
1. Go to your Visual Studio Solution (the one you intend to continously deploy to the Raspberry Pi).
2. Right click on the project and click on the menu item "Properties"
3. Go to the "Build Events" tab, and under Post-build events, enter the following: 
* `echo %DATE% %TIME% >> "$(TargetDir)sshdeploy.ready"`
	*This simply writes the date and time to the `sshdeploy.ready` file. Whenever this file CHANGES, the deployment tool will perform a deployment.
4. Open a Command Prompt (Start, Run, cmd, [Enter Key])
5. Navigate to  your project folder where the csproj file resides
* Example:`cd "C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\"`
6. Run this tool with some arguments. Here is an example so you can get started quickly.
```
    dotnet sshdeploy monitor -s "C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\bin\Debug" -t "/home/pi/target" -h 192.168.2.194 -u pi -w raspberry
```
* In the above command,
	* `-s` refers to the sorce path of the files to transfer.
	* `t` refers to the full path of the target directory.
	* `-h` refers to the host (IP address of the Raspberry Pi).
	* `-u` refers to the login.
	* `-w` refers to the password.
* Note that there are many more arguments you can use. Simply issue 
```
dotnet sshdeploy monitor
```
This will get you all the options you can use.

* If all goes well you will see output similar to this:
```
SSH Deployment Tool [Version 1.0.0.0]
(c) 2015 Unosquare SA de CV. All Rights Reserved.

Monitor mode starting
Monitor parameters follow:
    Monitor File    C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\bin\Debug\sshdeploy.ready
    Source Path     C:\projects\Unosquare.Labs.RasPiConsole\Unosquare.Labs.RasPiConsole\bin\Debug
    Excluded Files  .ready|.vshost.exe|.vshost.exe.config
    Target Address  192.168.2.194:22
    Username        pi
    Target Path     /home/pi/target
    Clean Target    YES
    Pre Deployment
    Post Deployment
Connecting to host 192.168.2.194:22 via SFTP.
File System Monitor is now running.
Writing a new monitor file will trigger a new deployment.
Remember: Press Q to quit.
Ground Control to Major Tom: Have a nice trip in space!
```
7. Now go back to your Visual Studio Solution, right click on the project, a select "Rebuild". You should see output in the command line similar to the following:
```
     Starting deployment ID 1 - Sunday, June 14, 2015 10:16:20 PM
     Cleaning Target Path '/home/pi/target'
     Deploying 3 files.
     Finished deployment in 0.88 seconds.
```
* Every time you rebuild your project, it will be automatically deployed!

* *In order to make this tool much more useful, we need to take advantage of the pre and post commands. The idea is to find the process and kill it if it is currently running on the pre-command, and run the process once the deployment has been completed using the post-command argument. The hope is thay this will make the deploy, run, and debug cycle, much less tedious for a .NET developer using a Raspberry Pi.*

* Here's a good example of using pre and post commands to acocmplish the above:
 ```dotnet sshdeploy monitor -s "C:\projects\libfprint-cs\trunk\Unosquare.Labs.LibFprint.Tests\bin\Debug" -t "/home/pi/libfprint-cs" -h 192.168.2.194 --pre "pgrep -f 'Unosquare.Labs.LibFprint.Tests.exe' | xargs -r kill" --post "mono /home/pi/libfprint-cs/Unosquare.Labs.LibFprint.Tests.exe" --clean False```
## References
### Monitor Mode


|Short Argument | Long Argument |               Description                              | Default      | Required|
|:-----:        | :-----------: | :----------------------------------------------------: | :-----------:| :-----------:|
|  -m           | --monitor     | The path to the file used as a signal that the files are ready to be deployed. Once the deploymetn is completed,the file is deleted. | sshdeploy.ready | :heavy_check_mark:|
|  -s           | --source      | The source path for the files to transfer              |              |:heavy_check_mark:|
|  -t           | --target      | The target path of the files to transfer               |              |:heavy_check_mark:|
|               | --pre         | Command to execute prior file transfer to target       |              | :x:          |
|               |--post         | Command to execute after file transfer to target | | :x:
|               | --clean       | Deletes all files and folders on the target before pushing the new files | True  |:x: |
|               | --exclude     | a pipe (\|) separated list of file suffixes to ignore while deploying.|.ready\|.vshost.exe\|.vshost.exe.config |:x:|
|  -v           | --verbose     |Add this option to print messages to standard error and standard output streams. | True | :x:|
|  -h           | --host        | Hostname or IP Address of the target. -- Must be running an SSH server. | |:heavy_check_mark:|
| -p            | --port        | Port on which SSH is running.                          | 22            | :x:          |
|  -u           |--username     | The username under which the connection will be established. |pi       | :x:          |
|  -w           | --password    |The password for the given username.                    | raspberry     | :x:          |
|  -l           | --legacy      | Monitor files using legacy method                      | False         | :x:          |

### Push Mode



|Short Argument | Long Argument |               Description                              | Default      | Required     |
|:-------------:| :-----------: | :----------------------------------------------------: | :-----------:| :-----------:|
| -c            |--configuration|  Target configuration.                                 | Debug        | :x:           | 
| -f            |--framework    |The source framework                                    |              |:heavy_check_mark:|  
|               | --pre         | Command to execute prior file transfer to target       |              | :x:           |
|               |--post         | Command to execute after file transfer to target | | :x:
|               | --clean       | Deletes all files and folders on the target before pushing the new files | True  |:x: |
|               | --exclude     | a pipe (\|) separated list of file suffixes to ignore while deploying.|.ready\|.vshost.exe\|.vshost.exe.config |:x:|
|  -v           | --verbose     |Add this option to print messages to standard error and standard output streams. | True | :x:|
|  -h           | --host        | Hostname or IP Address of the target. -- Must be running an SSH server. | |:heavy_check_mark:|
| -p            | --port        | Port on which SSH is running.                          | 22            | :x:          |
|  -u           |--username     | The username under which the connection will be established. |pi       |  :x:         |
|  -w           | --password    |The password for the given username.                    | raspberry     | :x:          |
  

## Special Thanks

This code uses the very cool Renci's <a href="https://github.com/sshnet/SSH.NET" target="_blank">SSH.NET library</a> and our awesome SWAN library available <a href="https://github.com/unosquare/swan" target="_blank">here</a>.
