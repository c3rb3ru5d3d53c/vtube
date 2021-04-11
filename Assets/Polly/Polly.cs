using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

public class Polly : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip audioClip;
    private HttpListener listener;
    private HttpListenerContext context;
    private Thread listenerThread;
    public string listenURL = "http://127.0.0.1:4444";
    private byte[] audioBytes;
    private int audioSize = 0;
    private bool bPlayAudio = false;

    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        listener = new HttpListener();
		listener.Prefixes.Add(listenURL);
        listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        listener.Start();
        listenerThread = new Thread(startListener);
        listenerThread.Start();
        Debug.Log("Polly Server Started\n");
    }

    IEnumerator PlayAudio()
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(listenURL, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            audioClip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = audioClip;
            audioSource.Play();
        }
    }

    private void startListener ()
	{
		while (true) {               
			var result = listener.BeginGetContext(ListenerCallback, listener);
			result.AsyncWaitHandle.WaitOne();
		}
	}

    private void ListenerCallback(IAsyncResult result)
	{
        context = listener.EndGetContext(result);
		if (context.Request.HttpMethod == "POST") {	
            audioBytes = new BinaryReader(context.Request.InputStream).ReadBytes((int)context.Request.ContentLength64);
            audioSize = (int)context.Request.ContentLength64;
            context.Response.Close();
            Debug.Log("Received");
            bPlayAudio = true;
		}
        if (context.Request.HttpMethod == "GET"){
            context.Response.ContentType = "audio/mpeg";
            System.IO.Stream stream = context.Response.OutputStream;
            stream.Write(audioBytes, 0, audioSize);
            context.Response.Close();
        }
	}

    // Update is called once per frame
    void Update()
    {
        if (bPlayAudio == true){
            StartCoroutine(PlayAudio());
        }
        bPlayAudio = false;
    }
}
