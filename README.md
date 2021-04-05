# Unity VTube Studio

This is my VTube Studio Unity Project

https://www.youtube.com/channel/UCASUdhRodl9IBbL3hB-L1Jw

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

Interestingly, Unity does not support `yuv` format but it does support `yuyv` format.
