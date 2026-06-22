using Godot;
using System;
using System.Collections.Generic;
using System.Data;

public partial class RightPanel : PanelContainer
{
    public enum RightPanelTabPage
    {
        Normal, Playing
    }

    [Export] private Control normalPanel, playingPanel;

	[Export] private TabContainer tabContainer;
	
	public override void _Ready()
    {
        tabContainer = GetNode<TabContainer>("TabContainer");
    }

    // 方法1：按索引切换到指定标签页
    public void SwitchToTab(int index)
    {
        if (index >= 0 && index < tabContainer.GetTabCount())
        {
            tabContainer.CurrentTab = index;
        }
    }

    // 方法2：按标签页的内容节点名称查找并切换
    public void SwitchToTab(string tabNodeName)
    {
        for (int i = 0; i < tabContainer.GetTabCount(); i++)
        {
            Control tabControl = tabContainer.GetTabControl(i);
            if (tabControl.Name == tabNodeName)
            {
                tabContainer.CurrentTab = i;
                break;
            }
        }
    }

    // 方法3：直接通过内容节点切换
    public void SwitchToTab(Control tabContent)
    {
        int index = tabContainer.GetTabIdxFromControl(tabContent);
        if (index != -1)
        {
            tabContainer.CurrentTab = index;
        }
    }

    public void SwitchToTab(RightPanelTabPage rightPanelTabPage)
    {
        switch (rightPanelTabPage)
        {
            case RightPanelTabPage.Normal:
                SwitchToTab(normalPanel);
                break;
            case RightPanelTabPage.Playing:
                SwitchToTab(playingPanel);
                break;
        }
    }
}
