using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyChat.Models;
using EasyChat.Utilities;

namespace EasyChat.ViewModels.SubVms;

public partial class UserListVm : ObservableObject
{
    private readonly BindingList<ChatModel> _sourceUsers = [];

    public Action<ChatModel>? OnSelected { get; set; }
    public Action<ChatModel>? RightClicked { get; set; }
    public Action<ChatModel>? AvatarClicked { get; set; }

    [ObservableProperty] private BindingList<ChatModel> _users = [];

    [ObservableProperty] private string _searchText = "";

    public IReadOnlyList<ChatModel> SourceUsers => _sourceUsers.ToList();

    partial void OnSearchTextChanged(string value)
    {
        RefreshFilter();
    }

    public void Add(ChatModel user)
    {
        _sourceUsers.Add(user);
        user.PropertyChanged += UserPropertyChanged;
        RefreshFilter();
    }

    public void AddOrUpdate(ChatModel user)
    {
        var existing = FindByUid(user.Uid);
        if (existing == null)
        {
            Add(user);
            return;
        }

        existing.NickName = user.NickName;
        existing.Image = user.Image;
        existing.IsOnline = user.IsOnline;
        existing.IsGroup = user.IsGroup;
        existing.IpAddress = user.IpAddress;
        existing.Port = user.Port;
        if (!string.IsNullOrWhiteSpace(user.GroupName))
        {
            existing.GroupName = user.GroupName;
        }
    }

    public ChatModel? FindByUid(string uid)
    {
        return _sourceUsers.FirstOrDefault(user => user.Uid == uid);
    }

    public void RemoveWhere(Func<ChatModel, bool> predicate)
    {
        for (var i = _sourceUsers.Count - 1; i >= 0; i--)
        {
            if (!predicate(_sourceUsers[i]))
            {
                continue;
            }

            _sourceUsers[i].PropertyChanged -= UserPropertyChanged;
            _sourceUsers.RemoveAt(i);
        }

        RefreshFilter();
    }

    public void SortBy(Func<ChatModel, object> primary, Func<ChatModel, object>? secondary = null)
    {
        var ordered = secondary == null
            ? _sourceUsers.OrderBy(primary).ToList()
            : _sourceUsers.OrderBy(primary).ThenBy(secondary).ToList();

        ResetSource(ordered);
    }

    public void SortByDescending(Func<ChatModel, object> primary, Func<ChatModel, object>? secondary = null)
    {
        var ordered = secondary == null
            ? _sourceUsers.OrderByDescending(primary).ToList()
            : _sourceUsers.OrderByDescending(primary).ThenBy(secondary).ToList();

        ResetSource(ordered);
    }

    public void RefreshFilter()
    {
        Users = new BindingList<ChatModel>(_sourceUsers
            .Where(user => ChatHelpers.MatchesSearch(user, SearchText))
            .ToList());
    }

    private void ResetSource(IEnumerable<ChatModel> users)
    {
        foreach (var user in _sourceUsers)
        {
            user.PropertyChanged -= UserPropertyChanged;
        }

        _sourceUsers.Clear();
        foreach (var user in users)
        {
            _sourceUsers.Add(user);
            user.PropertyChanged += UserPropertyChanged;
        }

        RefreshFilter();
    }

    private void UserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatModel.Message) && string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        if (e.PropertyName is nameof(ChatModel.NickName)
            or nameof(ChatModel.RemarkName)
            or nameof(ChatModel.DisplayName)
            or nameof(ChatModel.Uid)
            or nameof(ChatModel.Message))
        {
            RefreshFilter();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
    }

    [RelayCommand]
    private void Selected(ChatModel? user)
    {
        if (user != null)
        {
            OnSelected?.Invoke(user);
        }
    }

    [RelayCommand]
    private void RightClick(ChatModel? user)
    {
        if (user != null)
        {
            RightClicked?.Invoke(user);
        }
    }

    [RelayCommand]
    private void AvatarClick(ChatModel? user)
    {
        if (user != null)
        {
            AvatarClicked?.Invoke(user);
        }
    }
}
