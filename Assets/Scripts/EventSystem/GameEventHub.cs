using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton event hub để quản lý tất cả event trong game
/// </summary>
public class GameEventHub 
{
    public static GameEventHub Instance { get; private set; }
    
    private Dictionary<GameEventType, List<Action<object>>> eventListeners = new Dictionary<GameEventType, List<Action<object>>>();

    /// <summary>
    /// Static constructor - khởi tạo singleton instance
    /// </summary>
    static GameEventHub()
    {
        if (Instance == null)
        {
            Instance = new GameEventHub();
        }
    }
    
    /// <summary>
    /// Private constructor
    /// </summary>


    /// <summary>
    /// Đăng ký listener cho một event
    /// </summary>
    /// <param name="eventType">Loại event</param>
    /// <param name="listener">Hàm callback khi event được invoke</param>
    public void AddListener(GameEventType eventType, Action<object> listener)
    {
        if (!eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType] = new List<Action<object>>();
        }

        if (!eventListeners[eventType].Contains(listener))
        {
            eventListeners[eventType].Add(listener);
        }
    }

    /// <summary>
    /// Hủy đăng ký listener từ một event
    /// </summary>
    /// <param name="eventType">Loại event</param>
    /// <param name="listener">Hàm callback cần hủy</param>
    public void RemoveListener(GameEventType eventType, Action<object> listener)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType].Remove(listener);
        }
    }

    /// <summary>
    /// Trigger event - gọi tất cả listener đã đăng ký
    /// </summary>
    /// <param name="eventType">Loại event</param>
    /// <param name="data">Dữ liệu truyền vào callback</param>
    public void Invoke(GameEventType eventType, object data = null)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            foreach (var listener in eventListeners[eventType])
            {
                listener?.Invoke(data);
            }
        }
       
    }

    /// <summary>
    /// Xóa tất cả listener của một event
    /// </summary>
    /// <param name="eventType">Loại event</param>
    public void ClearListeners(GameEventType eventType)
    {
        if (eventListeners.ContainsKey(eventType))
        {
            eventListeners[eventType].Clear();
        }
    }

    /// <summary>
    /// Xóa tất cả listener của tất cả event
    /// </summary>
    public void ClearAllListeners()
    {
        eventListeners.Clear();
    }
}
