using Godot;
using QuickType;
using System;

namespace HPhiEditorGame.Editor
{
    /// <summary>强类型属性编辑器接口</summary>
    public interface IPropertyEditor
    {
        string Label { get; }
        Control Control { get; }
        event Action<string, object> ValueChanged;
        void SetValue(object value);
        void Setup(string label); 
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
        public string Label => _label;
        public Control Control => this;

        public abstract T Value { get; set; }
        public event Action<T> TypedValueChanged;
        public event Action<string, object> ValueChanged;

        protected void NotifyChanged(T newValue)
        {
            TypedValueChanged?.Invoke(newValue);
            ValueChanged?.Invoke(_label, newValue);
        }

        public void SetValue(object value) => Value = (T)value;

        public void Setup(string label)
        {
            _label = label;
            BuildUI();
        }

        protected abstract void BuildUI();
    }

    /// <summary>缓动数据（从原 InfoEditPanel 移出）</summary>
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