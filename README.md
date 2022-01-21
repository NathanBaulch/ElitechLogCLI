An unofficial command line tool for working with Elitech data logging devices.

Tested with the RC-4 and RC-5 devices compiled against ElitechLogWin versions 4.2.1, 5.0.0, and 6.2.0.

## Features
* Merge data readings into a single sequence without duplication.
* Stores data in a more compact unencrypted SQLite file.
* Download from multiple devices in quick succession.
* Export combined data for multiple devices in a variety of formats.

## Commands

```text
> .\ElitechLogCLI.exe

ElitechLogCLI 1.0.0
Copyright (C) 2022 ElitechLogCLI

  info       Display status information about the connected device
  pull       Pull the latest readings from the connected device
  chart      Display device readings in a simple chart
  set        Set parameters on the connected device
  migrate    Migrate data from the ElitechLogWin DB
  export     Export readings in the specified format
  reset      Delete all readings on the connected device
```

## Charts
```text
> .\ElitechLogCLI.exe chart

Serial number EFE199G58375, period from 2019-12-24 to 2021-04-20
 25.7 ┤      ╭╮                                                                        ╭╮
 25.0 ┤      ││╭╮                                                                      ││
 24.3 ┤╭╮ ╭╮ ││││                                                         ╭╮      ╭╮   ││
 23.7 ┤││ ││ │││╰╮                                                   ╭╮   ││   ╭╮ ││   ││
 23.0 ┼╯│╭╯╰─╯││ │    ╭╮                                            ╭╯╰─╮ ││   ││ │╰───╯│
 22.3 ┤ ╰╯    ╰╯ │    ││                                            │   │ ││  ╭╯╰╮│     │    ╭╮
 21.6 ┤          │╭─╮╭╯│╭╮                                          │   ╰╮│╰╮╭╯  ││     │  ╭─╯│╭╮
 20.9 ┤          ╰╯ ││ │││                                        ╭╮│    ││ ││   ╰╯     ╰╮ │  ││╰╮
 20.3 ┤             ╰╯ ││╰╮                                       │││    ╰╯ ╰╯           ╰─╯  ╰╯ │
 19.6 ┤                ╰╯ │                               ╭╮╭╮    │╰╯                            │
 18.9 ┤                   │╭──╮                           ││││╭─╮╭╯                              │
 18.2 ┤                   ╰╯  │                           │││││ ╰╯                               │╭
 17.6 ┤                       │                        ╭──╯││╰╯                                  ╰╯
 16.9 ┤                       ╰╮╭╮                     │   ││
 16.2 ┤                        │││                    ╭╯   ││
 15.5 ┤                        ╰╯│╭╮╭╮             ╭╮ │    ││
 14.9 ┤                          ╰╯│││ ╭──╮        ││ │    ╰╯
 14.2 ┤                            ╰╯╰╮│  │     ╭╮╭╯│ │
 13.5 ┤                               ╰╯  ╰──╮╭─╯││ ╰─╯
 12.8 ┤                                      ╰╯  ╰╯
```

## Installing from source
1. Install the latest ElitechLogWin application and take note of the install directory.
2. Open ElitechLogCLI.csproj in a text editor and replace all instances of "C:\tools\ElitechLogWin" with your install directory.
3. Compile the project.
4. Copy all binaries into the ElitechLogWin install directory.
5. Add the ElitechLogWin install directory to your path.
6. [Optional] Run the migrate command to copy existing ElitechLogWin readings into the local DB.

## Built with
* [ElitechLogWin](http://www.elitechlog.com/softwares/)
* [CommandLineParser](https://github.com/commandlineparser/commandline)
* [Dapper](https://github.com/DapperLib/Dapper)
* [Kurukuru](https://github.com/mayuki/Kurukuru)
* [ShellProgressBar](https://github.com/Mpdreamz/shellprogressbar)
* [AsciiChart.Sharp](https://github.com/samcarton/asciichart-sharp)
* [YamlDotNet](https://github.com/aaubry/YamlDotNet)
* [CsvHelper](https://joshclose.github.io/CsvHelper/)
* [Humanizer](https://github.com/Humanizr/Humanizer)
* [Chronic](https://github.com/mojombo/chronic)

## Future work
* Improved charts that properly display gaps in the readings and possibly multiple devices at once. 
* Better support for newer devices that use protocol V48 and the native USB interface.
* Localize in other languages.
