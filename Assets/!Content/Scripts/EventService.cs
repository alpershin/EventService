using System.Threading.Tasks;

namespace Analytics
{
    using System.Linq;
    using UnityEngine.Events;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Networking;
    using Random = UnityEngine.Random;

    public class EventService : MonoBehaviour
    {
        private readonly string _delayedEventsPrefsKey = "DelayedEvents";
        private readonly string _eventTypeFieldName = "Type";
        private readonly string _eventDataFieldName = "Data";
        
        [SerializeField] private string _url = string.Empty;
        
        private Queue<EventData> _events = new Queue<EventData>();
        private IEnumerator _postRoutine;
        private bool _isCanSendEventData = true;
        
        private float _cooldownBeforeSend => Random.Range(1f, 3f);
        
        private void Awake()
        {
            TryLoad();
            TrySendEvent();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) return;
            Save();
        }

        [ContextMenu("Test request")]
        public void TestEvent()
        {
            for (int i = 0; i < 100; i++)
            {
                TrackEvent(EventType.SpendCoins, "spendCoins");
            }
        }
        
        public void TrackEvent(EventType type, string data)
        {
            var eventData = new EventData(type, data);
            _events.Enqueue(eventData);

            TrySendEvent();
        }

        private bool TryLoad()
        {
            var savedData = PlayerPrefs.GetString(_delayedEventsPrefsKey, string.Empty);
            if (savedData == string.Empty) return false;

            var data = JsonUtility.FromJson<DelayedEventsWrapper>(savedData);
            data.DelayedEvents.ForEach(e => _events.Enqueue(e));

            return true;
        }

        [ContextMenu("Save")]
        private void Save()
        {
            var saveData = new DelayedEventsWrapper(_events.ToList());
            var json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(_delayedEventsPrefsKey, json);
        }

        private bool TrySendEvent()
        {
            if (!_isCanSendEventData || _events.Count <= 0) return false;
            
            _postRoutine = PostRoutine(_events.Peek(), CheckEventPostResult);
            StartCoroutine(_postRoutine);
            return true;
        }
        
        private IEnumerator PostRoutine(EventData data, UnityAction<UnityWebRequest.Result> result)
        {
            _isCanSendEventData = false;
            
            var dataPost = JsonUtility.ToJson(data, true);
            using UnityWebRequest request = UnityWebRequest.Post(_url, dataPost);
            
            yield return request.SendWebRequest();
            result.Invoke(request.result);
            
            yield return new WaitForSecondsRealtime(_cooldownBeforeSend);
            _isCanSendEventData = true;
        }

        private void CheckEventPostResult(UnityWebRequest.Result result)
        {
            Debug.Log($"Result: {result.ToString()}");
            
            if (result is UnityWebRequest.Result.Success)
                _events.TryDequeue(out EventData data);

            TrySendEvent();
        }
    }

    public enum EventType
    {
        LevelStart,
        LevelComplete,
        SpendCoins
    }
    
    [System.Serializable]
    public class EventData
    {
        public EventType Type;
        public string Data;

        public EventData(EventType type, string data)
        {
            Type = type;
            Data = data;
        }
    }

    [System.Serializable]
    public class DelayedEventsWrapper
    {
        public List<EventData> DelayedEvents;

        public DelayedEventsWrapper(List<EventData> delayedEvents)
        {
            DelayedEvents = delayedEvents;
        }
    }
}