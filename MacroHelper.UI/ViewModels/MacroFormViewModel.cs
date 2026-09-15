using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroHelper.Core.Entities;
using MacroHelper.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace MacroHelper.UI.ViewModels;

public partial class MacroFormViewModel : ObservableObject
{
    private readonly MacroService     _macroService;
    private readonly CategoriaService _catService;
    private readonly Macro?           _original;

    [ObservableProperty] private string  _atalho    = string.Empty;
    [ObservableProperty] private string  _titulo    = string.Empty;
    [ObservableProperty] private string  _conteudo  = string.Empty;
    [ObservableProperty] private int?    _categoriaId;
    [ObservableProperty] private bool    _ativo     = true;
    [ObservableProperty] private string? _atalhoTecla;
    [ObservableProperty] private string? _erroMensagem;
    [ObservableProperty] private string? _avisoDuplicata;
    [ObservableProperty] private bool    _isSaving  = false;
    [ObservableProperty] private ObservableCollection<Categoria> _categorias = new();

    // Realce de sintaxe: variáveis {nome} detectadas ao digitar
    [ObservableProperty] private ObservableCollection<string> _variaveisNoConteudo = new();
    public bool HaTokensDetectados => VariaveisNoConteudo.Count > 0;
    private static readonly Regex _variavelRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    partial void OnConteudoChanged(string value)
    {
        var variaveis = _variavelRegex.Matches(value)
            .Select(m => "{" + m.Groups[1].Value + "}")
            .Distinct().ToList();

        VariaveisNoConteudo = new ObservableCollection<string>(variaveis);
        OnPropertyChanged(nameof(HaTokensDetectados));
    }

    // Anexo de imagem (ex: assinatura)
    [ObservableProperty] private string? _imagemBase64;
    [ObservableProperty] private bool    _temImagem;

    // Histórico de versões
    [ObservableProperty] private ObservableCollection<MacroVersao> _historico = new();
    [ObservableProperty] private bool _mostrarHistorico = false;

    public bool   IsEdicao         => _original != null;
    public string TituloFormulario => IsEdicao ? "Editar Macro" : "Nova Macro";

    public event Func<Task>? Salvo;
    public event Action?     Cancelado;

    public MacroFormViewModel(MacroService macroService, CategoriaService catService, Macro? macro)
    {
        _macroService = macroService;
        _catService   = catService;
        _original     = macro;

        if (macro != null)
        {
            Atalho       = macro.Atalho;
            Titulo       = macro.Titulo;
            Conteudo     = macro.Conteudo;
            CategoriaId  = macro.CategoriaId;
            Ativo        = macro.Ativo;
            AtalhoTecla  = macro.AtalhoTecla;
            ImagemBase64 = macro.ImagemBase64;
            TemImagem    = macro.ImagemBase64 != null;
        }
        _ = CarregarCategoriasAsync();
    }

    private async Task CarregarCategoriasAsync()
    {
        var lista  = (await _catService.ObterTodosAsync()).ToList();
        lista.Insert(0, new Categoria { Id = 0, Nome = "Sem categoria" });
        Categorias  = new ObservableCollection<Categoria>(lista);
        if (CategoriaId == null) CategoriaId = 0;
    }

    [RelayCommand]
    public void ColarImagem()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsImage())
            {
                ErroMensagem = "Não há imagem na área de transferência.";
                return;
            }

            var img = System.Windows.Clipboard.GetImage();
            using var ms = new MemoryStream();
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(img));
            encoder.Save(ms);
            ImagemBase64 = Convert.ToBase64String(ms.ToArray());
            TemImagem    = true;
            ErroMensagem = null;
        }
        catch (Exception ex)
        {
            // O clipboard é compartilhado: OpenClipboard falha se outro processo o mantém
            // aberto. Antes o botão simplesmente não fazia nada, sem explicação nenhuma.
            App.LogErro(ex);
            ErroMensagem = "Não foi possível colar a imagem. Tente de novo em um instante.";
        }
    }

    [RelayCommand]
    public void RemoverImagem()
    {
        ImagemBase64 = null;
        TemImagem = false;
    }

    [RelayCommand]
    public async Task AbrirHistorico()
    {
        if (_original == null) return;
        Historico = new ObservableCollection<MacroVersao>(await _macroService.ObterVersoesAsync(_original.Id));
        MostrarHistorico = true;
    }

    [RelayCommand] public void FecharHistorico() => MostrarHistorico = false;

    [RelayCommand]
    public void RestaurarVersao(MacroVersao versao)
    {
        Titulo = versao.Titulo;
        Conteudo = versao.Conteudo;
        MostrarHistorico = false;
    }

    // ── Salvar ────────────────────────────────────────────────
    [RelayCommand]
    public async Task Salvar()
    {
        ErroMensagem = null; IsSaving = true;
        try
        {
            var categoriaSelecionada = Categorias.FirstOrDefault(c => c.Id == CategoriaId);
            var macro = new Macro
            {
                Id          = _original?.Id ?? 0,
                Atalho      = Atalho,
                Titulo      = Titulo,
                Conteudo    = Conteudo,
                CategoriaId = CategoriaId == 0 ? null : CategoriaId,
                // Id 0 é o item "Sem categoria" do combo: um rótulo de UI, não uma categoria.
                // Gravá-lo como texto faria o placeholder virar dado e aparecer no filtro.
                Categoria   = CategoriaId == 0 ? null : categoriaSelecionada?.Nome,
                Ativo       = Ativo,
                Favorito    = _original?.Favorito ?? false,
                AtalhoTecla = string.IsNullOrWhiteSpace(AtalhoTecla) ? null : AtalhoTecla.Trim(),
                ImagemBase64 = ImagemBase64
            };

            if (AvisoDuplicata == null)
            {
                var duplicata = await _macroService.DetectarPossivelDuplicataAsync(macro);
                if (duplicata != null)
                {
                    AvisoDuplicata = $"Conteúdo muito parecido com \"{duplicata.Titulo}\" (/{duplicata.Atalho}). Clique em Salvar novamente para confirmar.";
                    return;
                }
            }

            var (ok, msg, _) = await _macroService.SalvarAsync(macro);
            if (!ok) { ErroMensagem = msg; return; }
            if (Salvo != null) await Salvo();
        }
        finally { IsSaving = false; }
    }

    [RelayCommand] public void Cancelar() => Cancelado?.Invoke();
}
