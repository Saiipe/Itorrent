using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Itorrent.Core.Engine;
using Itorrent.Core.Security;
using Itorrent.Core.Stats;
using Itorrent.Desktop.Assets;

namespace Itorrent.Desktop.ViewModels;

/// <summary>
/// Nó da árvore de arquivos da tela de confirmação. Marcar uma pasta marca tudo dentro dela;
/// a pasta fica "parcial" quando só alguns filhos estão marcados.
/// </summary>
public sealed class FileNode : ObservableObject
{
    private readonly Action _onChanged;
    private bool? _isChecked = true;
    private bool _isExpanded;

    private FileNode(string name, FileNode? parent, Action onChanged, PreviewFile? file = null)
    {
        Name = name;
        Parent = parent;
        File = file;
        _onChanged = onChanged;
    }

    public string Name { get; }
    public FileNode? Parent { get; }
    public PreviewFile? File { get; }
    public ObservableCollection<FileNode> Children { get; } = [];

    public bool IsFolder => File is null;
    public long Length => File?.Length ?? Children.Sum(c => c.Length);
    public string SizeText => Format.Bytes(Length);
    public bool IsRisky => File is { Risk: not FileRisk.None };
    public ImageSource Icon => PixelIcons.Get(IsFolder ? "Folder" : IsRisky ? "Warning" : "File");

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetChecked(value, updateChildren: true, updateParent: true);
    }

    private void SetChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (value == _isChecked)
            return;
        _isChecked = value;

        if (updateChildren && value.HasValue)
        {
            foreach (var child in Children)
                child.SetChecked(value, true, false);
        }
        if (updateParent)
            Parent?.Recompute();

        OnPropertyChanged(nameof(IsChecked));
        _onChanged();
    }

    private void Recompute()
    {
        bool? state = Children.All(c => c.IsChecked == true) ? true
            : Children.All(c => c.IsChecked == false) ? false
            : null;
        SetChecked(state, false, true);
    }

    public IEnumerable<FileNode> Leaves() =>
        IsFolder ? Children.SelectMany(c => c.Leaves()) : [this];

    /// <summary>Monta a árvore a partir dos caminhos do torrent.</summary>
    public static FileNode BuildTree(TorrentPreview preview, Action onChanged)
    {
        var root = new FileNode(preview.Name, null, onChanged) { _isExpanded = true };
        foreach (var file in preview.Files)
        {
            var parts = file.Path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            var node = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var next = node.Children.FirstOrDefault(c => c.IsFolder && c.Name == parts[i]);
                if (next is null)
                {
                    next = new FileNode(parts[i], node, onChanged);
                    node.Children.Add(next);
                }
                node = next;
            }
            node.Children.Add(new FileNode(parts.Length > 0 ? parts[^1] : file.Path, node, onChanged, file));
        }

        // Pastas primeiro, depois arquivos, em ordem alfabética (como o Gerenciador de Arquivos).
        Sort(root);
        if (root.Children.Count <= 50)
        {
            foreach (var c in root.Children.Where(c => c.IsFolder))
                c._isExpanded = true;
        }
        return root;
    }

    private static void Sort(FileNode node)
    {
        var ordered = node.Children
            .OrderBy(c => c.IsFolder ? 0 : 1)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        node.Children.Clear();
        foreach (var c in ordered)
        {
            node.Children.Add(c);
            Sort(c);
        }
    }
}
