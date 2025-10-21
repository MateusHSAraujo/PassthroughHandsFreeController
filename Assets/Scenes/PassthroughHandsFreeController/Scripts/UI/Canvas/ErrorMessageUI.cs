using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ErrorMessageUI : TwoButtonsCameraGazeControlledCanvas
{
    [Header("Error Message UI configurations:")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI messageText;

    void Start()
    {
        printMessage("Hello world");
    }
    
    public void printMessage(string msg)
    {
        messageText.text = msg;
    }
}