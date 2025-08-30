using PassthroughCameraSamples;
using UnityEngine;

public class PassthrougCameraDisplay : MonoBehaviour
{

    [SerializeField]
    private WebCamTextureManager webcamTexMng;

    [SerializeField]
    private Renderer quad;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (webcamTexMng.WebCamTexture != null && quad!=null)
        {
            quad.material.mainTexture = webcamTexMng.WebCamTexture;
        }
    }
}
