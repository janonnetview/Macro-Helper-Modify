using System.Collections.ObjectModel;
using System.Dynamic;
using System.Windows.Input;
using MacroHelper.Core.Entities;

namespace MacroHelper.Tests;

// Contextos mutáveis apenas em memória: nenhum ViewModel que lê Settings ou inicia serviços.
internal sealed class SkaldDadosDeDiagnostico
{
    public ExpandoObject Contexto { get; } = new();
    public IDictionary<string, object?> Valores => Contexto;
    public List<ComandoContado> Comandos { get; } = new();
    public ObservableCollection<Nota> Notas { get; } = new();
    public ObservableCollection<Categoria> Categorias { get; } = new();
    public static readonly DateTime Data = new(2026, 9, 15, 10, 0, 0);

    public SkaldDadosDeDiagnostico(string tela, int quantidade, string tema)
    {
        foreach (var arquivo in new[] { "MainWindow", "ConfiguracoesView", "NotasView", "CategoriasView", "DashboardView" })
        foreach (var caminho in Xaml.CaminhosDeBinding(Xaml.Ler($"Views/{arquivo}.xaml")))
        {
            var nome = caminho.Replace("DataContext.", "", StringComparison.Ordinal);
            if (!nome.EndsWith("Command", StringComparison.Ordinal) || Valores.ContainsKey(nome)) continue;
            var comando = new ComandoContado(nome);
            Valores[nome] = comando;
            Comandos.Add(comando);
        }

        foreach (var nome in new[] { "AvatarMenuAberto", "MacrosExpandido", "MacrosPaginaAtiva", "MostrarEditor",
                     "EditorVisualizando", "EditorFixada", "MostrarFormulario", "IniciarComWindows",
                     "MinimizarParaBandeja", "SugestaoProativaIA", "HistoricoClipboardAtivo", "CarregandoSaude" })
            Valores[nome] = false;
        foreach (var nome in new[] { "Mensagem", "EditorErro", "FormErro", "CapturandoAtalho", "FormPaiId", "ConfirmandoExclusaoId", "CurrentView" })
            Valores[nome] = null;
        foreach (var nome in new[] { "TermoBusca", "AppsModoDigitacao" }) Valores[nome] = "";
        foreach (var nome in new[] { "TotalMacros", "TotalMacrosHealth", "MacrosNovasSemana", "UsadasHoje",
                     "MinutosEconomizados", "TarefasOcultas", "UsoCategoriaConfirmacao" }) Valores[nome] = quantidade;
        Valores["NomeUsuario"] = "Pessoa de teste";
        Valores["SeuNome"] = "Pessoa de teste";
        Valores["PaginaAtiva"] = tela.ToLowerInvariant();
        Valores["MacrosExpandido"] = tela == "Categorias";
        Valores["StatusMensagem"] = "Dados sintéticos";
        Valores["DataTexto"] = "TERÇA-FEIRA, 15 DE SETEMBRO";
        Valores["TemaSelecionado"] = tema == "Light" ? "Claro" : "Escuro";
        Valores["GatilhoPrefixo"] = "/";
        Valores["AtalhoBuscaTexto"] = "Ctrl + Espaço";
        Valores["AtalhoRepetirTexto"] = "Ctrl + Alt + V";
        Valores["CorAccentSelecionada"] = "Verde sálvia";
        Valores["CoresAccentDisponiveis"] = MacroHelper.Services.PaletaDeDestaque.Cores.Select(c =>
            new MacroHelper.UI.ViewModels.AmostraDeCor(c.Nome, c.ParaTema(tema == "Dark"))).ToList();
        foreach (var nome in new[] { "CpuTexto", "MemoriaTexto", "MemoriaSistemaTexto", "DiscoTexto" }) Valores[nome] = "Ensaio";
        Valores["VersaoAppTexto"] = "Diagnóstico";
        Valores["EditorTitulo"] = "Nota sintética";
        Valores["EditorConteudo"] = "Conteúdo sintético para medir o editor, sem dados pessoais.";
        Valores["FormTitulo"] = "Nova categoria";
        Valores["FormNome"] = "Categoria sintética";
        Valores["FormCor"] = "#74A68C";
        Valores["FormIcone"] = "\uE8B7";
        Valores["IconesDisponiveis"] = new[] { "\uE8B7", "\uE8A5", "\uE713" };
        for (var i = 1; i <= quantidade; i++)
        {
            Notas.Add(new Nota { Id = i, Titulo = $"Nota sintética {i:00}", Conteudo = "Resumo de exemplo para medição espacial.", DataCriacao = Data, DataAtualizacao = Data });
            Categorias.Add(new Categoria { Id = i, Nome = $"Categoria sintética {i:00}", Icone = "\uE8B7", Cor = "#74A68C", DataCriacao = Data,
                Subcategorias = new() { new Categoria { Id = 100 + i, Nome = $"Subcategoria {i:00}", Icone = "\uE8A5", Cor = "#74A68C", DataCriacao = Data } } });
        }
        Valores["Notas"] = Notas;
        Valores["Categorias"] = Categorias;
        Valores["CategoriasRaiz"] = Categorias;
        Valores["MinutosEconomizados"] = $"{quantidade}min";
        Valores["Compromissos"] = Enumerable.Range(1, quantidade == 18 ? 18 : 0).Select(i =>
            new Compromisso { Id = i, Titulo = $"Compromisso sintético {i}", Local = "Local de teste", Quando = Data.AddMinutes(i * 15) }).ToList();
        // Mesmos cortes do DashboardViewModel: ranking de cinco e seis tarefas visíveis.
        Valores["MaisUsadas"] = Enumerable.Range(1, Math.Min(5, quantidade)).Select(i => new ItemInicio { Titulo = $"Macro sintética {i}", Atalho = $"/teste{i}", Total = i }).ToList();
        Valores["Tarefas"] = Enumerable.Range(1, Math.Min(6, quantidade)).Select(i => new ItemInicio { Titulo = $"Tarefa sintética {i}" }).ToList();
        Valores["TarefasOcultas"] = Math.Max(0, quantidade - 6);
    }

    public sealed class ItemInicio
    {
        public string Titulo { get; set; } = "";
        public string Atalho { get; set; } = "";
        public int Total { get; set; }
        public DateTime Prazo { get; set; } = Data.AddDays(365);
        public string Prioridade { get; set; } = "Normal";
        public bool TemLembretePendente { get; set; }
        public DateTime? Lembrete { get; set; }
    }

    public sealed class ComandoContado(string nome) : ICommand
    {
        public string Nome { get; } = nome;
        public int Execucoes { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => Execucoes++;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
