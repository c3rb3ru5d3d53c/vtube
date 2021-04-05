using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VideoDevice : MonoBehaviour{
    private bool bDevice = false;
    public string deviceName;
    // Start is called before the first frame update
    void Start(){
        WebCamDevice[] devices = WebCamTexture.devices;
        for (int i = 0; i < devices.Length; i++){
            if (devices[i].name.Contains(deviceName)){
                deviceName = devices[i].name;
                bDevice = true;
                Debug.LogFormat("VideoDevice: {0} exists", deviceName);
                break;
            }
        }
        if (bDevice == false){
            Debug.LogFormat("VideoDevice: {0} does not exist", deviceName);
            return;
        }
        Renderer rend = this.GetComponentInChildren<Renderer>();
        WebCamTexture deviceTexture = new WebCamTexture(deviceName, 1920, 1080);
        //string camName = devices[0].name;
        rend.material.mainTexture = deviceTexture;
        deviceTexture.Play();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
