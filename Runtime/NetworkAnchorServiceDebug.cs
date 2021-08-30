using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Listens to the service debug events and displays them or logs them.
/// </summary>
public class NetworkAnchorServiceDebug : MonoBehaviour
{
    [SerializeField]
    private Text _debugText;

    [SerializeField] [Tooltip("Should the messages be printed into the log?")]
    private bool _log = true;

    // Start is called before the first frame update
    void Start()
    {
        NetworkAnchorService.Instance.OnDebugLogInfo+= OnDebugLogInfo; 
    }

    private void OnDebugLogInfo(string arg1, int arg2)
    {
        string debugText = arg1 + " : " + arg2;
        if(_debugText)
            _debugText.text = debugText;
        if(_log)
            Debug.Log(debugText);
    }

   
}
