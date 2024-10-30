using System;
using UnityEngine.Events;

namespace UNIArt.Editor
{
    public class ReactiveProperty<T>
    {
        private T _value;

        // 事件：当值改变时触发
        public UnityEvent<T> OnValueChanged = new UnityEvent<T>();

        // 构造函数，初始化默认值
        public ReactiveProperty(T initialValue = default)
        {
            _value = initialValue;
        }

        // 获取和设置值时触发事件
        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    OnValueChanged?.Invoke(_value); // 通知所有订阅者
                }
            }
        }

        public void SetValueWithoutNotify(T value)
        {
            _value = value;
        }

        public void SetValueAndForceNotify(T value)
        {
            _value = value;
            OnValueChanged?.Invoke(_value); // 通知所有订阅者
        }

        // 允许简便的隐式转换
        public static implicit operator T(ReactiveProperty<T> property)
        {
            return property._value;
        }
    }
}
