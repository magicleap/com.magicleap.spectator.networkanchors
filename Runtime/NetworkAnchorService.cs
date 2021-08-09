using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkAnchorService : MonoBehaviour
{
    //The Main Network Anchor
    public NetworkAnchor NetworkAnchor { get; private set; }
    //The main Player's Pcfs
    private PlayerPcfReference _mainPlayerPcfReference;

    //Network Response actions
    public Action<UploadCoordinatesResponse> OnCoordinatesUploaded;
    public Action<CreateAnchorResponse> OnNetworkAnchorCreated;
    public Action<SharedAnchorResponse> OnReceiveNetworkAnchor;
    public Action<GetHostCoordinatesResponse> OnReceiveHostedCoordinates;

    /// <summary>
    /// Function used to send the network events to the client and server
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target for the Network event. int[0] = All , int[1]{-1} = MasterClient , int[i>0] = Target Player Id's </param>
    public delegate void BroadcastNetworkEvent(byte networkEventCode, string jsonData, int[] players);
    public BroadcastNetworkEvent OnBroadcastNetworkEvent;

    public bool NetworkAnchorIsValid
    {
        get { return NetworkAnchor != null && !string.IsNullOrEmpty(NetworkAnchor.AnchorId); }
    }

    private PlayerPcfReference _localPcfReferences;

    //Client Request Event Codes
    public const byte UploadCoordinatesRequestEventCode = 101;
    public const byte CreateAnchorRequestEventCode = 102;
    public const byte SharedAnchorRequestEventCode = 103;
    public const byte GetHostCoordinatesRequestEventCode = 104;

    //Server Responses
    public class UploadCoordinatesResponse
    {
        public const byte EventCode = 11;
        public ResultCode ResponseCode;
    }
    public class CreateAnchorResponse
    {
        public const byte EventCode = 12;
        public ResultCode ResponseCode;
        public NetworkAnchor NetworkAnchor;
    }
    public class SharedAnchorResponse
    {
        public const byte EventCode = 13;
        public ResultCode ResponseCode;
        public NetworkAnchor NetworkAnchor;
    }
    public class GetHostCoordinatesResponse
    {
        public const byte EventCode = 14;
        public ResultCode ResponseCode;
        public PlayerPcfReference PlayerPcfReference;
    }

    //Client request tasks. 
    TaskCompletionSource<UploadCoordinatesResponse> _uploadCoordinatesCompletionSource;
    TaskCompletionSource<CreateAnchorResponse> _createNetworkAnchorCompletionSource;
    TaskCompletionSource<SharedAnchorResponse> _sharedAnchorRequestCompletionSource;
    TaskCompletionSource<GetHostCoordinatesResponse> _downloadHostCoordinatesCompletionSource;

    //Response Result Codes
    public enum ResultCode
    {
        UNKNOWN = 0,
        SUCCESS,
        EXISTS,
        MISSING_INFORMATION,
        MISSING_ANCHOR,
        MISSING_SHARED_COORDINATE,
        MISSING_COORDINATES,
        FAILED = 100
    }



    //Process Network Events
    public void ProcessNetworkEvents(byte eventCode, object jsonData)
    {
        #region Client

        if (eventCode == SharedAnchorResponse.EventCode)
        {
            SharedAnchorResponse result = JsonUtility.FromJson<SharedAnchorResponse>((string)jsonData);
            _sharedAnchorRequestCompletionSource.TrySetResult(result);
            OnReceiveNetworkAnchor?.Invoke(result);
        }

        if (eventCode == CreateAnchorResponse.EventCode)
        {
            CreateAnchorResponse result = JsonUtility.FromJson<CreateAnchorResponse>((string)jsonData);
            _createNetworkAnchorCompletionSource.TrySetResult(result);
            OnNetworkAnchorCreated?.Invoke(result);
        }

        if (eventCode == UploadCoordinatesResponse.EventCode)
        {
            UploadCoordinatesResponse result = JsonUtility.FromJson<UploadCoordinatesResponse>((string)jsonData);
            _uploadCoordinatesCompletionSource.TrySetResult(result);
            OnCoordinatesUploaded?.Invoke(result);
        }

        if (eventCode == GetHostCoordinatesResponse.EventCode)
        {
            GetHostCoordinatesResponse result = JsonUtility.FromJson<GetHostCoordinatesResponse>((string)jsonData);
            _downloadHostCoordinatesCompletionSource.TrySetResult(result);
            OnReceiveHostedCoordinates?.Invoke(result);
        }

        #endregion

        #region Server

        if (eventCode == UploadCoordinatesRequestEventCode)
        {
            ProcessCoordinatesUpload((string)jsonData);
        }

        if (eventCode == CreateAnchorRequestEventCode)
        {
            ProcessNetworkAnchorCreation((string)jsonData);
        }

        if (eventCode == SharedAnchorRequestEventCode)
        {
            ProcessGetSharedNetworkAnchorRequest((string)jsonData);
        }

        if (eventCode == GetHostCoordinatesRequestEventCode)
        {
            ProcessGetHostCoordinatesRequest((string)jsonData);
        }
        #endregion
    }

    //Client Requests Logic
    public async Task<SharedAnchorResponse> SendGetSharedNetworkAnchorRequest(string playerId, List<GenericCoordinateReference> pcfIds)
    {
        _sharedAnchorRequestCompletionSource = new TaskCompletionSource<SharedAnchorResponse>();

        var playerReferenceCoordinates = new PlayerPcfReference()
        { PlayerId = playerId, CoordinateReferences = pcfIds };

        _localPcfReferences = playerReferenceCoordinates;

        SendNetworkEvent(SharedAnchorRequestEventCode, JsonUtility.ToJson(playerReferenceCoordinates), new int[] { -1 });

        while (!_sharedAnchorRequestCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _sharedAnchorRequestCompletionSource.Task.Result;
    }

    public async Task<UploadCoordinatesResponse> SendUploadCoordinatesRequest(string playerId, List<GenericCoordinateReference> pcfIds)
    {
        _uploadCoordinatesCompletionSource = new TaskCompletionSource<UploadCoordinatesResponse>();
        _localPcfReferences = new PlayerPcfReference() { PlayerId = playerId, CoordinateReferences = pcfIds };

        SendNetworkEvent(UploadCoordinatesRequestEventCode, JsonUtility.ToJson(_localPcfReferences), new int[] { -1 });

        while (!_uploadCoordinatesCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _uploadCoordinatesCompletionSource.Task.Result;
    }

    public async Task<CreateAnchorResponse> SendCreateNetworkAnchorRequest(NetworkAnchor networkAnchor)
    {
        _createNetworkAnchorCompletionSource = new TaskCompletionSource<CreateAnchorResponse>();

        SendNetworkEvent(CreateAnchorRequestEventCode, JsonUtility.ToJson(networkAnchor), new int[1] { -1 });

        while (!_createNetworkAnchorCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _createNetworkAnchorCompletionSource.Task.Result;
    }

    public async Task<GetHostCoordinatesResponse> SendDownloadHostCoordinatesRequest(string playerId)
    {
        _downloadHostCoordinatesCompletionSource = new TaskCompletionSource<GetHostCoordinatesResponse>();

        SendNetworkEvent(GetHostCoordinatesRequestEventCode, playerId, new int[1] { -1 });

        while (!_downloadHostCoordinatesCompletionSource.Task.IsCompleted)
            await Task.Delay(100);

        return _downloadHostCoordinatesCompletionSource.Task.Result;
    }
    //End

    //Server Responses Logic
    private void ProcessCoordinatesUpload(string playerCoordinatesJson)
    {
        var playerCoordinates = JsonUtility.FromJson(playerCoordinatesJson, typeof(PlayerPcfReference)) as PlayerPcfReference;
        if (playerCoordinates == null)
        {
            Debug.LogError($"A player has uploaded invalid data as coordinates {playerCoordinatesJson}");
        }
        else if (playerCoordinates.CoordinateReferences.Count == 0)
        {
            string result = JsonUtility.ToJson(new UploadCoordinatesResponse()
            { ResponseCode = ResultCode.MISSING_COORDINATES });

            SendNetworkEvent(UploadCoordinatesResponse.EventCode, result, new int[] { int.Parse(playerCoordinates.PlayerId) });

        }
        else if (_mainPlayerPcfReference == null || _mainPlayerPcfReference.CoordinateReferences.Count == 0 || _mainPlayerPcfReference.PlayerId == playerCoordinates.PlayerId)
        {
            _mainPlayerPcfReference = playerCoordinates;
            string result = JsonUtility.ToJson(new UploadCoordinatesResponse()
            { ResponseCode = ResultCode.SUCCESS });

            SendNetworkEvent(UploadCoordinatesResponse.EventCode, result, new int[] { int.Parse(playerCoordinates.PlayerId) });

        }
        else
        {
            string result = JsonUtility.ToJson(new UploadCoordinatesResponse()
            { ResponseCode = ResultCode.EXISTS });

            SendNetworkEvent(UploadCoordinatesResponse.EventCode, result, new int[] { int.Parse(playerCoordinates.PlayerId) });
        }
    }

    private void ProcessNetworkAnchorCreation(string networkAnchorJson)
    {
        var targetAnchor = JsonUtility.FromJson(networkAnchorJson, typeof(NetworkAnchor)) as NetworkAnchor;

        bool isValid = !string.IsNullOrEmpty(targetAnchor.AnchorId)
                       && targetAnchor.LinkedCoordinate != null
                       && !string.IsNullOrEmpty(targetAnchor.LinkedCoordinate.CoordinateId);

        if (!NetworkAnchorIsValid)
        {
            if (!isValid)
            {
                string result = JsonUtility.ToJson(new CreateAnchorResponse()
                { ResponseCode = ResultCode.MISSING_INFORMATION, NetworkAnchor = targetAnchor });

                SendNetworkEvent(CreateAnchorResponse.EventCode, result, new[] { int.Parse(targetAnchor.OwnerId) });
            }
            else
            {
                NetworkAnchor = targetAnchor;

                string result = JsonUtility.ToJson(new CreateAnchorResponse()
                { ResponseCode = ResultCode.SUCCESS, NetworkAnchor = targetAnchor });

                SendNetworkEvent(CreateAnchorResponse.EventCode, result, new int[0]);
            }
        }
        else
        {

            string result = JsonUtility.ToJson(new CreateAnchorResponse()
            { ResponseCode = isValid ? ResultCode.EXISTS : ResultCode.FAILED, NetworkAnchor = targetAnchor });

            SendNetworkEvent(CreateAnchorResponse.EventCode, result, new[] { int.Parse(targetAnchor.OwnerId) });
        }
    }

    private void ProcessGetSharedNetworkAnchorRequest(string playerReferenceCoordinates)
    {
        GenericCoordinateReference coordinateReference = null;
        PlayerPcfReference playerReference = JsonUtility.FromJson<PlayerPcfReference>(playerReferenceCoordinates) as PlayerPcfReference;

        //If we do not have an active anchor return false
        if (NetworkAnchor == null)
        {
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_ANCHOR });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
            return;
        }

        if (_mainPlayerPcfReference == null || _mainPlayerPcfReference.CoordinateReferences.Count == 0)
        {
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_COORDINATES });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
            return;
        }

        var pcfIds = playerReference.CoordinateReferences.Select(x => x.CoordinateId).ToList();
        var coordinateReferences = _mainPlayerPcfReference.CoordinateReferences;
        for (int i = 0; i < coordinateReferences.Count; i++)
        {
            if (pcfIds.Contains(coordinateReferences[i].CoordinateId))
            {
                coordinateReference = coordinateReferences[i];
                break;
            }
        }

        if (coordinateReference == null)
        {
            // TODO: Network call to the players to get new anchors and try again?
            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = null, ResponseCode = ResultCode.MISSING_SHARED_COORDINATE });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
        }
        else
        {
            Debug.Log("Network Anchor 1 " + JsonUtility.ToJson(NetworkAnchor));
            var resultAnchor = new NetworkAnchor(NetworkAnchor.AnchorId, coordinateReference, NetworkAnchor.GetWorldPosition(),NetworkAnchor.GetWorldRotation());
            
            Debug.Log("Result Network Anchor 2 " + JsonUtility.ToJson(resultAnchor));

            string anchorResultJson = JsonUtility.ToJson(new SharedAnchorResponse() { NetworkAnchor = resultAnchor, ResponseCode = ResultCode.SUCCESS });
            SendNetworkEvent(SharedAnchorResponse.EventCode, anchorResultJson, new[] { int.Parse(playerReference.PlayerId) });
        }
    }

    private void ProcessGetHostCoordinatesRequest(string playerId)
    {
        if (_mainPlayerPcfReference != null && _mainPlayerPcfReference.CoordinateReferences.Count > 0)
        {
            var resultData = new GetHostCoordinatesResponse()
            {
                PlayerPcfReference = _mainPlayerPcfReference,
                ResponseCode = ResultCode.SUCCESS
            };

            SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(playerId) });
        }
        else
        {
            var resultData = new GetHostCoordinatesResponse()
            {
                ResponseCode = ResultCode.MISSING_COORDINATES
            };

            SendNetworkEvent(GetHostCoordinatesResponse.EventCode, JsonUtility.ToJson(resultData), new[] { int.Parse(playerId) });
        }
    }
    //End

  

    /// <summary>
    /// Function used to send the network events to the client and server
    /// </summary>
    /// <param name="networkEventCode">The network event code that is referenced to ensure the current parser for the data</param>
    /// <param name="jsonData">The event data json file that will be parsed</param>
    /// <param name="players">The target for the Network event. int[0] = All , int[1]{-1} = MasterClient , int[i>0] = Target Player Id's </param>
    private void SendNetworkEvent(byte networkEventCode, string jsonData, int[] players)
    {
        OnBroadcastNetworkEvent?.Invoke(networkEventCode, jsonData, players);
    }

}
