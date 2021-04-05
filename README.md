# Unity VTube Studio

This is my VTube Studio Unity Project

https://www.youtube.com/channel/UCASUdhRodl9IBbL3hB-L1Jw

# Installing

To use this project you must first download `UnityHub`.
```
wget https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub.AppImage -O unityhub
chmod +x unityhub && mv unityhub /usr/bin
```

After `UnityHub` is downloaded, you will need to clone this repository.

```
git clone --recursive https://github.com/c3rb3ru5d3d53c/vtube.git
```

Once all this is completed, you can then launch `UnityHub` and open the project.

# Screen Sharing

To share you screen on a plane in the a scene, create a plane and add the the `Assets/Scripts/VideoDevice.cs` script as a component to the object.

Then you must drag and drop the `Assets/Scripts/VideoDevice.mat` on the plane.

When this is done, you have to do the following:

```bash
sudo apt install -y v4l2loopback-dkim ffmpeg
sudo modprobe v4l2loopback
Tools/screenshare.sh <monitor-number> <dummy-video-device>
```

Once `screenshare.sh` is running, then using the `VideoDevice.cs` script, you must set the `deviceName` to something like `/dev/video2` where the dummy `v4l2loopback` device was created.

It will then be possible to click `Play` in `Unity` and the screen sharing should be visible.

Interestingly, Unity does not support `yuv` format but it does support `yuyv` format.
