#!/bin/bash

if [[ "$USER" != "root" ]]; then
  su root -c "$0"
  exit 0
fi

htan_runner_lib="/usr/lib/htan-runner"
$temp_file="/tmp/htan-runner.zip"

if [[ -f "$htan_runner_lib/HTAN.Runner.exe" ]]; then
  echo "On this server is already installed HTAN.Runner."
  exit 0
fi

# install mono
if [[ ! dpkg-query -s "mono-devel" 2> /dev/null | grep -q "ok installed" ]]; then
	echo "Installing Mono …"
	apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
	echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list
	echo "deb http://download.mono-project.com/repo/debian wheezy-apache24-compat main" | tee -a /etc/apt/sources.list.d/mono-xamarin.list
	echo "deb http://download.mono-project.com/repo/debian wheezy-libjpeg62-compat main" | tee -a /etc/apt/sources.list.d/mono-xamarin.list

	apt-get install -y mono-devel mono-complete ca-certificates-mono

	echo "Done."
fi

# download htan-runner
echo "Downloading …"
if [[ -f "$temp_file" ]]; then
  rm $temp_file
fi

wget -O $temp_file https://github.com/adminstock/htan-runner/releases/download/v1.0.3/Binnary.zip

echo "Done."

# create folders for HTAN.Runner
echo "Creating folders …"
if [[ ! -d "$htan_runner_lib" ]]; then
  mkdir -p $htan_runner_lib
fi

if [[ ! -d "/etc/htan/app-available" ]]; then
  mkdir -p /etc/htan/app-available
fi

if [[ ! -d "/etc/htan/app-enabled" ]]; then
  mkdir -p /etc/htan/app-enabled
fi

echo "Done."

if [[ ! dpkg-query -s "unzip" 2> /dev/null | grep -q "ok installed" ]]; then
	echo "Installing uzip …"
	apt-get install -y unzip
	echo "Done."
fi

# unzipping
unzip $temp_file -d $htan_runner_lib
rm $temp_file

# install hatan-runner
echo "Installing HTAN.Runner …"
if [[ -f "/etc/init.d/htan-runner" ]]; then
  update-rc.d -f htan-runner remove
	rm /etc/init.d/htan-runner
fi

cp $htan_runner_lib/htan-runner /etc/init.d/htan-runner && /
chmod u=rx,g=rx /etc/init.d/htan-runner && /
update-rc.d htan-runner defaults

echo "Done."