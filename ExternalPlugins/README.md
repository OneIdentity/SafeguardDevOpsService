# Installion, configuration and development of external plugins for Safeguard Secrets Broker for DevOps

## Installation

To install a plugin, click on the  ```Upload Custom Plugin``` button in the ```What would you like to plug in?``` card. This will display a file selection window that can be used to select a plugin installation file. Once a plugin intallation file as been selected, Secrets Broker will install the plugin and display a new card that can be used to provide addition connection configuration.

Updating a plugin is done in the same way. To update a plugin, click on the ```Upload Custom Plugin``` button and select the new plugin installation file. Updating a plugin will require a restart of the Secrets Broker service.

All of the plugins for Secrets Broker are available on the SafeguardDevOpsService Github project site (<https://github.com/OneIdentity/SafeguardDevOpsService>). Plugins can be downloaded by clicking on the ```View Plugin Repository``` button in the ```What would you like to plug in?``` card.

## Configuration

Once installed, a plugin can be configured by click on the ```Edit Configuration``` link in the respective plugin card. This will cause Secrets Broker to display the ```Plugin Settings``` page.

The Plugin Settings page contains three configuration panels.

**Basic Info** - This panel displays basic information about the plugin which includes the plugin name, description and version. The panel is readonly.

**Configuration** - This panel is used to configure the plugin with connection information. All plugins need to select an account/password that is used to access the third party secret store. The account and password must have been created in SPP prior to configuring the plugin. A checkbox is provided to set the enabled state of the plugin. If ```Disabled``` is checked, the plugin will not monitor SPP for password changes.

* ***Plugin Details*** - Each plugin has a set of plugin specific configuration. Please see the README.md for each individual plugin for addition conifiguration information.

* ***Test Configuration*** - Once the plugin has been completely configured, use the ```Test Configuration``` button to test whether the plugin is able to complete a connection to the third party secret store.

**Managed Accounts** - This panel is used to select all of the accounts from SPP whose passwords should be pushed to the respective secrets storage. Click on the ```Select Accounts``` button to display a list of available accounts from SPP. Each selected account will be displayed in the ```Managed Accounts``` list. Once an account has been selected, an alternate account name can be defined. By default, the plugin will construction an account name by concatenating the asset name with the account name (MyAsset-MyAccount). The constructed name is what will appear in the secrets storage as the account. If an alternate account name is specified, the plugin will push the password to the secrets storage as the alternate account name.

**Other Functions**

* ***Delete Plugin*** - If the plugin is not longer needed or used, it can be removed from Secrets Broker by clicking on the ```Delete Plugin``` button.

* ***Save*** - To save all of the plugin configuration to the Secrets Broker database, click on the ```Save``` button.

## Developing external plugins for Safeguard Secrets Broker for DevOps

The Safeguard Secrets Broker for DevOps is an open source component that can be deployed as a service or as a container in a customer environment and includes plugins that can be added to communicate with various DevOps technologies.  This service discovers A2A secrets that are configured to be pushed to different DevOps secrets solutions. Pushing the secrets to the various DevOps secrets solutions or third party vaults, is the function of the external plugins.

An external plugin is a simple intermediary between the Safeguard Secrets Broker and a third party vault or any technology that stores secrets. Building an external plugin requires the plugin developer to implement a predefined plugin template with the specific functionality for communicating with the third party vault. The Safeguard Secrets Broker for DevOps project provides working plugins as well as a simple example plugin that can be used as a reference for building a new plugin. A plugin cannot be developed independent of the Safeguard Secrets Broker itself. The plugin developer will be required to clone the entire Safeguard Secrets Broker project from Github, load the project in Microsoft Visual Studio and add the new plugin into the project. Once the plugin has been developed and tested, the plugin can be installed into a running Safeguard Secrets Broker service as well as submitted back to the Safeguard Secrets Broker Github project for inclusion into the source code repository.

## Getting Started

The first step in getting started with developing a new plugin is to clone the Safeguard Secrets Broker for Devops project from Github. There is plenty of documentation on the Github site itself which walks through how to clone a project.  Once the project has been cloned and opened in Microsoft Visual Studio, the plugin developer is ready to start implementing their own plugin.

The Safeguard Secrets Broker solution contain multiple projects. These projects include:

* **SafeguardDevOpsService** - The main Safeguard Secrets Broker for Devops service.
* **DevOpsPluginCommon** - This project defines the interface between the Safeguard Secrets Broker service and the external plugins. The ILoadablePlugin.cs file contains the interface that must be implemented by each plugin.
* **ExternalPlugins** - This folder contains each of the currently implemented external plugins. Any of the plugins can be used as an example of building a custom external plugin. The SmsTextEmail plugin is a sample plugin that can be used as a template for new plugin development. (**Note:** The SmsTextEmail plugin should never be used in a production environment. It is sample code only.)
* **SetupSafeguardDevOpsServer** - This project builds the SafeguardDevOpsServer MSI installable package.

### Parts of a Plugin

* Each plugin in the ExternalPlugins folder is a separate Visual Studio project that is part of the entire solution. A plugin must be developed within this environment as a new Visual Studio project.
* PluginDescriptor.cs - This file is the implementation of the ILoadablePlugin interface. The name of this file and the corresponding class must remain as defined.
* Manifest.json - Each plugin must include a Manifest.json file. This file defines the following for each plugin:

  * Name - Name of the external plugin. This name must match the name of the built plugin DLL.
  * DisplayName - Display name of the plugin that will appear in the Web user interface.
  * Assembly - Name of the built plugin DLL.
  * Version - Plugin version.

### Plugin Interface

* **GetPluginInitialConfiguration()** - Returns a Dictionary that defines the configuration elements that are required by the plugin. The configuration of every plugin is defined as key/value pairs.
* **SetPluginConfiguration()** - This method is called whenever a new configuration is updated by calling PUT /service/devops/v1/Plugins/{name} API or when the plugin is initially loaded by the Safeguard Secrets Broker service.
* **SetVaultCredential()** - This method is called before the TestVaultConnection() method is called or the Safeguard Secrets Broker A2A monitor is enabled. The implementation of this method should establish an authenticated connection with the third party vault and store the connection in memory to be used whenever credentials need to be pushed to the vault.
* **SetPassword()** - This method is called after the monitor has been enabled, the Safeguard Secrets Broker has been notified that a credential change has happened and the new credential needs to be pushed to the corresponding vault.  The implementation of this method should use the established connection to the vault to push the new credential under the specified account name.
* **SetLogger()** - This method is called when the plugin is initially loaded by the Safeguard Secrets Broker. It is called with the Safeguard Secrets Broker logger reference so that each plugin can write to the same logger as the Safeguard Secrets Broker itself.
* **TestVaultConnection()** - This method is called whenever the API /service/devops/v1/Plugins/{name}/TestConnection is called. The implementation of the method should use the authenticated connection that was established when the SetVaultCredentials() method was called and test the connectivity to the third party vault.
* **Unload()** - This method is called whenever the Safeguard Secrets Broker service is restarted or shutdown. The implementation of this method should include anything that needs to be done to the plugin to cleanly shutdown.

### Plugin Dependencies

In many, if not most, cases a third party vault may have a C# client library available to facilitate the interaction between the Safeguard Secrets Broker plugin and the actual vault. Make sure to take advantage of these client libraries when developing a new plugin.

The Safeguard Secrets Broker is configured to build as a .Net Core 3.1 console application. This means that all external plugins should also be built as .Net Core 6.0 assemblies. The plugin .csproj project file should contain the following in the \<PropertyGroup> section.

```Configuration
  <TargetFramework>net6.0</TargetFramework>
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  <Platforms>x64</Platforms>
```

In order for a plugin to build correctly, the plugin must include a project reference to the DevOpsPluginCommon project that is part of the SafeguardDevOpsService solution. In addition, each plugin along with all of its dependencies, must be compressed into a zip file. When a plugin is installed into the Safeguard Secrets Broker, the plugin must be uploaded through the web user interface as a zip file. All plugins that are part of the SafeguardDevopsService solution include some additional build \<Target\>'s in the plugin project's corresponding .csproj file that instructs the build to compress the plugin and dependencies into a zip file. These same \<Target>'s can be copied from an existing plugin .csproj file and pasted into the new plugin project file. The build \<Target>'s references a PowerShell script that is part of the Safeguard Secrets Broker solution and compresses the plugin and dependencies into a zip file.

```configuration
  <Target Name="PostBuildWindows" AfterTargets="PostBuildEvent">
    <Exec Command="powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Unrestricted -Command &quot;&amp; '$(SolutionDir)zipplugin.ps1' '$(ProjectDir)' '$(OutDir)' '$(SolutionDir)ExternalPlugins\bin\$(Configuration)\$(ProjectName).zip'&quot;"
          EchoOff="False" IgnoreExitCode="False" LogStandardErrorAsError="True" ContinueOnError="False" ConsoleToMsBuild="True" />
  </Target>
  <Target Name="PostBuildLinux" AfterTargets="PostBuildEvent" Condition="'$(OS)' != 'Windows_NT' And '$(SelfContained)' == 'true'" >
    <Exec Command="$(SolutionDir)zipplugin.sh $(ProjectDir) $(OutDir) $(SolutionDir)ExternalPlugins\bin\$(Configuration)\$(ProjectName).zip"
          EchoOff="False" IgnoreExitCode="False" LogStandardErrorAsError="True" ContinueOnError="False" ConsoleToMsBuild="True" />
  </Target>
```

### Plugin Testing

A plugin cannot be tested directly from Microsoft Visual Studio unless the plugin project file is changed to relocate the built plugin assemblies. Safeguard Secrets Broker looks for all plugins in the %ProgramData%/SafeguardDevOpsService/ExternalPlugins/\<your-plugin> folder. A plugin can be installed into this location in one of two ways. The first is by simply using the /service/devops/v1/plugins/File API. This API as well as all other Safeguard Secrets Broker APIs can be invoked using the Swagger API web page. This page can be accessed any time that the Safeguard Secrets Broker service is running. The page is available at `https://\<ip-address>/service/devops/swagger`. The second way to install the plugin is to navigate to the %ProgramData%/SafeguardDevOpsService/ExternalPlugins and create a new folder with the same name as the "Name" field in the plugin Manifest file. Then copy and paste the built assembly along with all of the dependencies that are contained in the plugin project's output folder. If the plugin is to be tested or debugged using the Visual Studio debugger, make sure to build and copy the debug version of the assemblies and dependencies. Once the plugin has been copied to the ExternalPlugins folder, the Safeguard Secrets Broker can be started in the Visual Studio debugger and debugged like any other console application. Every time that a change is made to the plugin code and a new plugin assembly has been built, copying and pasting the plugin assemblies will need to be repeated.

Remote debugging is also possible when testing or debugging a plugin. The same process as described above will need to be done on the system that is running the Safeguard Secrets Broker service. However, the service will need to be stopped before the plugin assemblies can be copied. Also the system that is running the service will need to have the Microsoft Remote Debugger installed. Once this is done and the Safeguard Secrets Broker is restarted, the plugin can be debugged by attaching to the remote system attaching to the SafeguardDevOpsService application.
