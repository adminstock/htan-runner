# HTAN.Runner

Daemon to run and manage **FastCGI** processes, mainly **fastcgi-mono-server**.

## Features

* Automatic start and stop **FastCGI** processes;
* Recovering processes;
* Designed as service;
* Starting the process on behalf of any user;
* Support for event processing start and stop individual processes;
* Threads based.

## License

**HTAN.Runner** is licensed under the **Apache License Version 2.0**.

## Requirements

* Debian 7 or 8;
* Mono >= 4.2.2.

_**NOTE:** Earlier versions have not been tested._

## Install

You can use the automatic installation through [HTAN](https://github.com/adminstock/htan):

```bash
su -l root
chmod u=rx,g=rx /usr/lib/htan/installers/htan-runner
/usr/lib/htan/installers/htan-runner
```

Or installation through `install.sh`:

```bash
su -l root
chmod u=rx,g=rx ./install.sh
./install.sh
```

## Uninstall

To remove daemon use command:

```bash
sudo update-rc.d -f htan-runner remove && sudo rm -r /etc/init.d/htan-runner
```

## Using

The principle work of **HTAN.Runner** is similar to **Nginx** or **Apache**.

Create in the folder `/etc/htan/app-available` configuration files for your applications.

For example, file `/etc/htan/app-available/example.conf`:
```xml
<configuration>
  <fastCGI>
    <add address="unix:/tmp/example.org" command="fastcgi-mono-server4" />
  </fastCGI>
  <commands>
    <add name="fastcgi-mono-server4" 
         exec="fastcgi-mono-server4" 
         arguments="/applications=/:/home/example.org/www/ /socket={socket} /multiplex=True /verbose=True" 
    />
  </commands>
</configuration>
```

To activate the application, create a symbolic link:

```bash
sudo ln -s /etc/htan/app-available/example.conf /etc/htan/app-enabled
```

For the changes to take effect, restart **htan-runner**:

```bash
sudo service htan-runner reload
```

## Structure of configuration files

The configuration files must be in **XML** format.

Recommended to use the extension `.conf` for file names.

All nodes must be in the root node `configuration`.

### fastCGI

The `fastCGI` node contains a list of addresses to start.

<table>
  <thead>
    <tr>
      <th>Parameter</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>address</td>
      <td>
        Specifies the address to listen on.<br />
        Valid values are "pipe", "unix", and "tcp".<br />
        For example: <code>unix:/tmp/example.org</code>, <code>tcp:127.0.0.1:9100</code>.<br />
        The address will be replaced by substituted for the marker <code>{socket}</code> in the specified command.
      </td>
    </tr>
    <tr>
      <td>command</td>
      <td>
        Command or command name (<code>&lt;commands /&gt;</code>) which should be run via <code>start-stop-daemon</code>.<br />
        For example:<br />
        <ul>
          <li><code>myCommandName</code></li>
          <li><code>/usr/bin/fastcgi-mono-server4 /applications=/:/home/example/www/ /socket={socket}</code></li>
        </ul> 
      </td>
    </tr>
    <tr>
      <td>
        beforeStartingCommand<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Command, command name or URL to be executed before executing the <code>command</code>.<br />
        For example: <br />
        <ul>
          <li><code>myCommandName</code></li>
          <li><code>echo "Starting..." >> custom.log</code></li>
          <li><code>http://api.foxtools.ru/v2/QR.html?mode=Auto&text=Hello+world%21&details=1</code></li>
        </ul>
      </td>
    </tr>
    <tr>
      <td>
        afterStartingCommand<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Command, command name or URL to be executed after starting the <code>command</code>.<br />
        For example: <br />
        <ul>
          <li><code>myCommandName</code></li>
          <li><code>echo "Started" >> custom.log</code></li>
          <li><code>http://example.org/</code></li>
        </ul>
      </td>
    </tr>
    <tr>
      <td>
        beforeStoppingCommand<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Command, command name or URL to be executed before stopping.
      </td>
    </tr>
    <tr>
      <td>
        afterStoppingCommand<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Command, command name or URL to be executed after stopping.
      </td>
    </tr>
    <tr>
      <td>
        stoppingTimeout<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Maximum waiting time stopping the process (in seconds). Default: <code>10</code> seconds.
      </td>
    </tr>
  </tbody>
</table>

### commands

The `commands` node contains a list of available commands.

<table>
  <thead>
    <tr>
      <th>Parameter</th>
      <th>Description</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>name</td>
      <td>
        Command name. Any convenient set of characters.<br />
        For example: <code>myCommandName</code>.
      </td>
    </tr>
    <tr>
      <td>exec</td>
      <td>
        Command line to be executed.<br />
        For example:<br />
        <ul>
          <li><code>service nginx reload</code></li>
          <li><code>echo "Hello world!" | mail -s "Test message" -t "example@example.org"</code></li>
          <li><code>/usr/bin/fastcgi-mono-server4</code></li>
          <li><code>/usr/bin/fastcgi-mono-server4 /applications=/:/home/example/www/ /socket={socket}</code></li>
          <li><code>echo "Any command"</code></li>
        </ul>
      </td>
    </tr>
    <tr>
      <td>
        arguments<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Additional arguments that will be passed to the command.<br />
        For example:<br />
        <ul>
          <li><code>-n -e</code></li>
          <li><code>/applications=/:/home/example/www/ /socket={socket}</code></li>
        </ul>
      </td>
    </tr>
    <tr>
      <td>
        user<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        User name under which the command is executed.<br />
        Default: <code>root</code>.
      </td>
    </tr>
    <tr>
      <td>
        group<br />
        <small><em>(optional)</em></small>
      </td>
      <td>
        Group name under which the command is executed.<br />
        Default: <code>root</code>.
      </td>
    </tr>
  </tbody>
</table>

## Disable application

To disable the application, just to remove a symbolic link and restart the service:

```bash
sudo rm /etc/htan/app-enabled/example.conf
sudo service htan-runner reload
```

## Log

Log of the program can be found at: `/var/log/htan/runner.log`

## See Also

* [Change Log](CHANGELOG.md)
* [Hosting Tools (HTAN)](https://github.com/adminstock/htan)
* [SmallServerAdmin (SSA)](https://github.com/adminstock/ssa)