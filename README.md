

# com.magicleap.spectator.networkanchors
A lightweight package that makes creating colocation experiences easier using a shared origin.

## Table of Contents
- [Getting Started (Photon)](#getting-started-photon)
- [Technical Information](#technical-information)
- [Additional Networking Solutions](#add-additional-networking-solutions)
- [Extend Coordinate Providers](#extend-coordinate-providers)
- [Troubleshooting](#troubleshooting)
- [Tips](#Tips)

## Install Guide
This package can be installed by cloning or unzipping this repository into your projects `Packages` folder or by importing the package using the Package Manager, by doing the following:

1. Copy the git URL from this project [Code Url](https://github.com/magicleap/com.magicleap.spectator.networkanchors.git).
2. Inside of your Unity project, open the Package Manager.
3. Select the + button and click **Add packages from git URL**.
4. Paste the project URL copied in step one and then click **Add**.

## Getting Started (Photon)
This section includes steps to create a multi-user experience using Photon and sample scripts provided in the package.

#### Download the examples
After Adding the Network Anchors package to the package manager, you will be able to install the examples via the package manager by selecting the package and expanding the dropdown title **Samples**.

### Enable Magic Leap Privileges 
1. Open the Player Settings Window
2. Select MagicLeap/Manifest settings
3. Enable the **PcfRead**, **Internet** and **Camera Capture**.

#### Enable the Photon Examples
1. Add downloading PUN 2 from the asset store and importing it into your project.
2. Define the your Photon credentials in using the **PUN Wizard** or **PUN Server settings**.
3. To enable the photon example scripts, navigate to the Player Settings  (**Edit > Project Settings** , then click **Player**).
4. Under the **Other Settings** section, add **PHOTON** to the **Scripting Define Symbols**. Do this for each of your target platforms settings (Standalone and Lumin). 

#### Create a Simple Scene
1. From the menu select File> New Scene. Then select the Basic (Built-in) template.
2. Replace the existing main camera with the Magic Leap Camera Prefab located under `Packages/Magic Leap SDK/Tools/Prefabs`.
3. Save the new scene, name it `NetworkAnchorsExample`.

#### Create the Photon Controller
1. Create an empty GameObject named `PhotonController`
2. Add the `Simple Photon Room` and `Simple Photon Lobby` components.
3. Set the Simple Photon Room's `Photon User Prefab` to the `SimplePhotonUser` prefab located under `/Assets/Samples/Network Anchors/1.1.0/Examples/PhotonExample/Resources/`

#### Implementing Network Anchors
1. Create an empty GameObject called `NetworkAnchor`
2. Set the transform's position to `0, 0, 1` and the rotation to `0, 18, 0`.
3. Add the `Network Anchor Service` component to the object. 
4. Then and the `Network Anchor Localizer` component.
5. Use the Network Anchor's Localizer's `OnAnchorPlaced` event to visualize when the anchor has been created/localized:
    1. Create a cube as a child of the object
    2. Set the scale to `0.3, 0.3, 0.3` and disable it. 
    3. Select the NetworkAnchor
    4. Create a new OnAnchorPlaced event
    5. Set the event target as the Cube and the event as `GameObject.SetActive`, set it to true.
1. Add the MultiPlatformCoordinateProvider prefab from `/Assets/Samples/Network Anchors/1.1.0/Examples/PhotonExample/Prefabs` into the scene.

#### Connect the Service to Photon
1. Create an empty GameObject called PunNetworkAnchorController
2. Add the `PhotonNetworkAnchorController` component.
3. Set the fields with the objects in your scene.

#### Call Create or Find Network Anchor
1. Select the Main Camera Prefab and add the `Magic Leap Network Anchor Example` component.
2. Set the Network Anchor Localizer field to target the Network Anchor object in your scene.

#### Build and Run
- Save your example scene.
- From the Project settings, enable the Internet privileges.
- Set your project's project Identification Information.
- Target your developer certificate in the project settings.
- Deploy to device. 

#### User Controls 
**Magic Leap**  
To create or find a network anchor on the Magic Leap Headsets, press the trigger on the controller.  

When  Auto Search for Image is disabled on the MLCoordinateProvider, Image tracking can be initialized using by pressing the trigger on the Controller. When initialized, the Magic Leap will try to locate an Image target for 1 minute.

**Desktop**  
If you are running the game on your Desktop, without Zero Iteration, enable `Force Standalone` on the MultiPlatformCoordinateProvider object in your scene. You can then enter play mode and hit spacebar. Localizing on your desktop will preserve the network anchors position even when the owner of the network anchor leaves.

## Technical Information

**Network Anchor Service**  
The entry point for localization requests. When information is requested, the services appends any required data (such as the players coordinates) and then invokes the BroadcastNetworkEvent.   

*Note:*
In order to make this solution flexible, the service does not communicate with the network directly. The network events should be caught, then relayed to your preferred network solution. Network events are sent as a json string.

**Network Anchor Service**  
Sends user input to the service and responds to the Network Anchor Service's localization events. 

**IGenericCoordinateProvider**  
An interface that needs to be implemented per platform so that the service can retrieve the platform's coordinates using a generic class.

**StandaloneCoordinateProvider**  
Implements IGenericCoordinateProvider and returns a remote players coordinates by first requesting them through the NetworkAnchor service, and then using them as it's own. This may be changed to use image targets in the future.

**MLGenericCoordinateProvider**  
Implements IGenericCoordinateProvider and returns the Magic Leap's PCFs as a Generic Coordinate.

##  Add Additional Networking Solutions

The samples folder contains a script called `GenericNetworkAnchorController`. This script can be used as a template for integrating other network solutions such as Mirror.
  - To make sure that player position's spawn properly, make sure that remote players are created as a child of the network anchor and transmit their local position. 
  - The network anchor service is only responsible for aligning the network anchor position across devices. Logic for creating and syncing transforms will have to be configured individually.

## Extend Coordinate Providers
Additional Coordinate providers can be added by implementing the `IGenericCoordinateProvider` interface. The samples folder includes a `MultiPlatformCoordinateProvider` which demonstrates a rudimentary way on providing the correct provider to the network service at runtime.

Coordinate providers are located in `Packages/com.magicleap.spectatorview.networkanchors/Runtime/Providers/` . The provided scripts implement the `RequestCoordinateReferences` as an `Async Task`. This is so that the task can be waited on until initialization is complete. View `MLGenericCoordinateProvider` to learn more.

## Troubleshooting
If the headsets cannot localize, follow the following troubleshooting steps.
* Make sure that you do not press **Skip** when initializing the headset, and that the headset showed a confirmation that the location was recognized.
* Enable the **Shared World** feature in the devices **Settings > Privacy** menu.
* Restart your headset.

## Tips
If you do not expect to localize with mobile devices, you can disable searching for an image target to make localization faster. To do this:
1. Select the MultiPlatformCoordinateProvider prefab then select the MLGenericCoordinateProvier child object. 
2. In the inspector, disable Auto Search for Image.

Note : Image scanning can still be triggered using an external script. See the MagicLeapNetworkAnchorExample.cs script for reference.