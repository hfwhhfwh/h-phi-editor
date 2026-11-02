using Godot;
using QuickType;
using System;

namespace HPhiEditorGame.Editor
{
    /// <summary>强类型属性编辑器接口</summary>
    public interface IPropertyEditor : IEditorField
    {
        string Label { get; }
        void Setup(string label, Func<object> readValue);
    }

    public interface IPropertyEditor<T> : IPropertyEditor
    {
        T Value { get; set; }
        event Action<T> TypedValueChanged;
    }

    /// <summary>抽象基类，自动处理 Godot 节点生命周期和事件通知</summary>
    public abstract partial class PropertyEditorBase<T> : MarginContainer, IPropertyEditor<T>
    {
        private string _label;
        private Func<object> _readValue;

        public string Label => _label;
        public Control View => this;

        public abstract T Value { get; set; }
        public event Action<T> TypedValueChanged;
        public event Action<EditorChange> ValueChanged;

        /// <summary>只应由用户交互回调调用；刷新路径绝不调用它。</summary>
        protected void NotifyChanged(T newValue)
        {
            TypedValueChanged?.Invoke(newValue);
            ValueChanged?.Invoke(new EditorChange(Array.Empty<string>(), newValue));
        }

        public void Setup(string label, Func<object> readValue)
        {
            _label = label;
            _readValue = readValue;
            BuildUI();
        }

        public object ReadValue() => _readValue?.Invoke();

        public void SetValueSilently(object value) => Value = (T)value;

        protected abstract void BuildUI();
    }

    /// <summary>缓动数据</summary>
    public class EasingData
    {
        public EasingIO EasingIO { get; set; }
        public EasingFunc EasingFunc { get; set; }
        public float EasingLeft { get; set; }
        public float EasingRight { get; set; }

        public EasingData Duplicate() => new()
        {
            EasingIO = EasingIO,
            EasingFunc = EasingFunc,
            EasingLeft = EasingLeft,
            EasingRight = EasingRight
        };
    }
}