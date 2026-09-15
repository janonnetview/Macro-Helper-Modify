using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MacroHelper.UI.Views;

/// <summary>
/// A tarefa aberta, numa janela ao lado da busca rápida — irmã da <see cref="NotaFlutuanteWindow"/>
/// e com as mesmas regras: grava sozinha ao sair, troca de tarefa sem fechar, e só grava se
/// alguma coisa mudou de verdade.
///
/// Traz os campos todos, e não só o título: uma tarefa sem prazo nem lembrete não é a mesma
/// tarefa, e mandar a pessoa abrir o app para ajustar a data anula o motivo do atalho existir.
/// </summary>
public partial class TarefaFlutuanteWindow : Window
{
    private readonly TarefaService _svc;
    private readonly IRelogio      _relogio;

    private Tarefa? _tarefa;
    private Assinatura _assinaturaOriginal;
    private bool    _confirmandoExclusao;

    /// <summary>
    /// Tudo que a pessoa pode ter mexido — é o que diz se houve edição de verdade.
    ///
    /// Um record e não texto concatenado: sem separador não há campo que possa imitar outro,
    /// e a igualdade estrutural vem de graça. Virou tipo nomeado quando ganhou o sétimo campo;
    /// como tupla inline, a mesma lista de tipos precisava ser repetida em três lugares.
    /// </summary>
    private readonly record struct Assinatura(
        string Titulo, string Observacoes, PrioridadeTarefa Prioridade,
        RecorrenciaTarefa Recorrencia, string Prazo, string LembreteData,
        string LembreteHora, bool Concluida);

    /// <summary>Gravou ou apagou — a lista da busca rápida precisa se refazer nos dois casos.</summary>
    public event Action? ListaMudou;

    public TarefaFlutuanteWindow(TarefaService svc, IRelogio relogio)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _svc     = svc;
        _relogio = relogio;

        Deactivated += (_, _) => GravarSeMudou();
    }

    public Tarefa? TarefaAtual => _tarefa;

    /// <summary>Abre uma tarefa existente. Chamada de novo com outra, grava a anterior e troca.</summary>
    public void Abrir(Tarefa tarefa)
    {
        if (_tarefa != null && !ReferenceEquals(_tarefa, tarefa)) GravarSeMudou();
        Carregar(tarefa, "Salva sozinha ao fechar");
        MostrarSePreciso(focoNoTitulo: false);
    }

    /// <summary>Abre uma tarefa em branco. Sem título escrito, nada é gravado ao fechar.</summary>
    public void Nova()
    {
        GravarSeMudou();
        Carregar(new Tarefa { Prioridade = PrioridadeTarefa.Normal }, "Escreva o que precisa ser feito");
        MostrarSePreciso(focoNoTitulo: true);
    }

    private void Carregar(Tarefa tarefa, string estado)
    {
        _tarefa = tarefa;

        TxtTitulo.Text       = tarefa.Titulo;
        TxtObservacoes.Text  = tarefa.Observacoes ?? string.Empty;
        CampoPrazo.Texto     = tarefa.Prazo?.ToString("dd/MM/yyyy") ?? string.Empty;
        CampoLembreteData.Texto = tarefa.Lembrete?.ToString("dd/MM/yyyy") ?? string.Empty;
        TxtLembreteHora.Text = tarefa.Lembrete?.ToString("HH:mm") ?? string.Empty;

        RbBaixa.IsChecked  = tarefa.Prioridade == PrioridadeTarefa.Baixa;
        RbNormal.IsChecked = tarefa.Prioridade == PrioridadeTarefa.Normal;
        RbAlta.IsChecked   = tarefa.Prioridade == PrioridadeTarefa.Alta;

        SelecionarRecorrencia(tarefa.Recorrencia);

        MarcarCaixaDeConclusao(tarefa.Concluida);

        // Tarefa que ainda não existe não tem o que apagar — nem lembrete que se possa adiar.
        BtnApagar.Visibility   = tarefa.Id == 0 ? Visibility.Collapsed : Visibility.Visible;
        PainelAdiar.Visibility = tarefa.Id == 0 ? Visibility.Collapsed : Visibility.Visible;
        DesarmarExclusao();

        Estado.Mostrar(estado);
        _assinaturaOriginal = AssinaturaAtual();
    }

    /// <summary>
    /// O Tag do botão guarda o estado como TEXTO, e não como bool: o DataTrigger do template
    /// compara com a string "True", e num Tag do tipo object não há conversão para acontecer.
    /// </summary>
    private void MarcarCaixaDeConclusao(bool concluida) =>
        BtnConcluir.Tag = concluida ? "True" : "False";

    private bool CaixaMarcada => (string?)BtnConcluir.Tag == "True";

    private void MostrarSePreciso(bool focoNoTitulo)
    {
        if (!IsVisible)
        {
            Show();
            Activate();
        }

        if (focoNoTitulo)
        {
            TxtTitulo.Focus();
            TxtTitulo.CaretIndex = TxtTitulo.Text.Length;
        }
    }

    /// <summary>Esconde gravando. Hide e não Close: a janela é reaproveitada na próxima tarefa.</summary>
    public void Fechar()
    {
        GravarSeMudou();
        _tarefa = null;
        Hide();
    }

    // ── Gravação ─────────────────────────────────────────────────────────────

    private Assinatura AssinaturaAtual() =>
        new(TxtTitulo.Text, TxtObservacoes.Text, PrioridadeEscolhida(), RecorrenciaEscolhida(),
            CampoPrazo.Texto, CampoLembreteData.Texto, TxtLembreteHora.Text, CaixaMarcada);

    private PrioridadeTarefa PrioridadeEscolhida() =>
        RbAlta.IsChecked  == true ? PrioridadeTarefa.Alta  :
        RbBaixa.IsChecked == true ? PrioridadeTarefa.Baixa :
                                    PrioridadeTarefa.Normal;

    /// <summary>
    /// Lê a recorrência pelo NOME guardado no Tag do item, e não pelo índice da lista.
    ///
    /// Por índice funcionaria hoje e quebraria calada no dia em que alguém reordenasse os
    /// itens no XAML: "Todo mês" viraria "Toda semana" em toda tarefa recorrente já gravada.
    /// </summary>
    private RecorrenciaTarefa RecorrenciaEscolhida() =>
        CmbRecorrencia.SelectedItem is System.Windows.Controls.ComboBoxItem { Tag: string nome }
        && Enum.TryParse<RecorrenciaTarefa>(nome, out var valor)
            ? valor
            : RecorrenciaTarefa.Nenhuma;

    private void SelecionarRecorrencia(RecorrenciaTarefa recorrencia)
    {
        var nome = recorrencia.ToString();

        foreach (var item in CmbRecorrencia.Items.OfType<System.Windows.Controls.ComboBoxItem>())
        {
            if ((string?)item.Tag != nome) continue;
            CmbRecorrencia.SelectedItem = item;
            return;
        }

        CmbRecorrencia.SelectedIndex = 0;
    }

    private void GravarSeMudou()
    {
        if (_tarefa == null) return;
        if (AssinaturaAtual() == _assinaturaOriginal) return;

        // Título em branco é tarefa abandonada: a nova que ninguém chegou a escrever, ou a
        // existente que o serviço recusaria de qualquer jeito. Sair sem gravar é o certo.
        var titulo = TxtTitulo.Text.Trim();
        if (titulo.Length == 0) return;

        var tarefa = _tarefa;
        var aviso  = string.Empty;

        // Data que não dá para entender NÃO vira null: manter o valor anterior preserva o
        // resto da edição em vez de apagar em silêncio um prazo que já existia.
        var prazo = LerData(CampoPrazo.Texto, tarefa.Prazo, "Prazo", ref aviso);

        DateTime? lembrete;
        if (string.IsNullOrWhiteSpace(CampoLembreteData.Texto))
        {
            lembrete = null;
        }
        else
        {
            var data = LerData(CampoLembreteData.Texto, tarefa.Lembrete?.Date, "Lembrete", ref aviso);
            var hora = DataDigitada.InterpretarHora(TxtLembreteHora.Text)
                       ?? tarefa.Lembrete?.TimeOfDay
                       ?? TimeSpan.FromHours(9);
            lembrete = data?.Add(hora);
        }

        // Aviso depois do prazo o serviço recusa (Tarefa.LembreteDepoisDoPrazo), e aqui uma
        // recusa perderia a edição inteira: esta janela grava sozinha ao sair, sem ninguém
        // olhando o rodapé. Então vale a mesma saída da data que não dá para entender — fica
        // o lembrete anterior, se ele ainda couber no prazo, e o aviso diz o que aconteceu.
        //
        // Só vale para o par que está mudando agora, pelo mesmo motivo do serviço: uma tarefa
        // com lembrete adiado para depois do prazo continua editável, e adiar não se desfaz
        // sozinho na próxima vez que a janela gravar.
        var parNovo = prazo != tarefa.Prazo || lembrete != tarefa.Lembrete;

        if (parNovo && prazo.HasValue && lembrete?.Date > prazo.Value.Date)
        {
            lembrete = tarefa.Lembrete?.Date <= prazo.Value.Date ? tarefa.Lembrete : null;
            aviso    = lembrete.HasValue
                ? "Lembrete depois do prazo. Mantive o anterior."
                : "Lembrete depois do prazo. Não gravei.";
        }

        tarefa.Titulo        = titulo;
        tarefa.Observacoes   = string.IsNullOrWhiteSpace(TxtObservacoes.Text) ? null : TxtObservacoes.Text.Trim();
        tarefa.Prioridade    = PrioridadeEscolhida();
        tarefa.Recorrencia   = RecorrenciaEscolhida();
        tarefa.Prazo         = prazo;
        tarefa.Lembrete      = lembrete;
        tarefa.Concluida     = CaixaMarcada;
        tarefa.DataConclusao = tarefa.Concluida ? tarefa.DataConclusao ?? _relogio.Agora : null;

        _assinaturaOriginal = AssinaturaAtual();
        GravarAsync(tarefa, aviso);
    }

    private DateTime? LerData(string texto, DateTime? anterior, string campo, ref string aviso)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var data = DataDigitada.Interpretar(texto, _relogio.Agora);
        if (data != null) return data;

        aviso = $"{campo} não entendido. Mantive o anterior.";
        return anterior;
    }

    private async void GravarAsync(Tarefa tarefa, string aviso)
    {
        try
        {
            var (ok, msg, _) = await _svc.SalvarAsync(tarefa);
            if (!ok) { Estado.Falhar(msg); return; }

            if (ReferenceEquals(_tarefa, tarefa))
            {
                // O serviço apara o título; o campo mostra o que ficou gravado de verdade.
                TxtTitulo.Text       = tarefa.Titulo;
                BtnApagar.Visibility = Visibility.Visible;
                _assinaturaOriginal  = AssinaturaAtual();
                // O aviso conta o que ficou de fora: âmbar, e não o cinza de quem só gravou.
                if (aviso.Length > 0) Estado.Avisar(aviso);
                else                  Estado.Mostrar($"Salva às {_relogio.Agora:HH:mm}");
            }

            ListaMudou?.Invoke();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    // ── Exclusão ─────────────────────────────────────────────────────────────

    private void DesarmarExclusao()
    {
        _confirmandoExclusao = false;
        BtnApagar.Content    = "Apagar";
    }

    private async void BtnApagar_Click(object sender, RoutedEventArgs e)
    {
        if (_tarefa is not { Id: > 0 }) return;

        // Dois cliques, e o botão diz o que o segundo vai fazer. Um clique só é pouco para uma
        // tarefa que não vai para lixeira nenhuma.
        if (!_confirmandoExclusao)
        {
            _confirmandoExclusao = true;
            BtnApagar.Content    = "Apagar mesmo?";
            Estado.Mostrar("Clique de novo para apagar.");
            return;
        }

        var alvo = _tarefa;

        // Zerado ANTES de esconder: o Deactivated dispara a gravação, e sem isso a tarefa
        // seria regravada no caminho para ser apagada.
        _tarefa = null;
        Hide();

        try
        {
            var (ok, msg) = await _svc.ExcluirAsync(alvo.Id);
            if (!ok) App.LogErro(new InvalidOperationException(msg));
            ListaMudou?.Invoke();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    // ── Teclado e botões ─────────────────────────────────────────────────────

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Com a exclusão armada, Esc desarma em vez de fechar: é a saída de quem clicou
            // em Apagar sem querer.
            if (_confirmandoExclusao)
            {
                DesarmarExclusao();
                Estado.Mostrar("Salva sozinha ao fechar");
            }
            else
            {
                Fechar();
                Owner?.Activate();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            GravarSeMudou();
            e.Handled = true;
        }
    }

    // ── Adiar o lembrete ─────────────────────────────────────────────────────

    private void BtnAdiar15_Click(object sender, RoutedEventArgs e) => Adiar(TimeSpan.FromMinutes(15));
    private void BtnAdiar1h_Click(object sender, RoutedEventArgs e) => Adiar(TimeSpan.FromHours(1));

    /// <summary>Amanhã às 9h — não "daqui a 24 horas", que cairia na mesma hora ruim de hoje.</summary>
    private void BtnAdiarAmanha_Click(object sender, RoutedEventArgs e)
        => Reagendar(_relogio.Agora.Date.AddDays(1).AddHours(9));

    private void Adiar(TimeSpan quanto) =>
        AplicarNovoLembrete(id => _svc.AdiarLembreteAsync(id, quanto));

    private void Reagendar(DateTime quando) =>
        AplicarNovoLembrete(id => _svc.ReagendarLembreteAsync(id, quando));

    /// <summary>
    /// Grava o que estiver editado, pede o novo lembrete ao serviço e relê a tarefa.
    ///
    /// Passa pelo serviço em vez de escrever nos campos e chamar o Salvar: é lá que mora a
    /// regra de rearmar a notificação, e o adiamento tem de valer também para um lembrete que
    /// já disparou — que é o caso que faz esta funcionalidade existir.
    ///
    /// A releitura no fim não é enfeite: sem ela os campos da tela continuariam mostrando o
    /// horário antigo, e a assinatura de edição consideraria isso uma alteração pendente —
    /// o próximo clique fora da janela regravaria o lembrete velho por cima do novo.
    /// </summary>
    private async void AplicarNovoLembrete(Func<int, Task<(bool Ok, string Msg)>> operacao)
    {
        if (_tarefa is not { Id: > 0 } alvo) return;

        try
        {
            GravarSeMudou();

            var (ok, msg) = await operacao(alvo.Id);
            if (!ok) { Estado.Falhar(msg); return; }

            var atualizada = await _svc.ObterPorIdAsync(alvo.Id);
            if (atualizada != null) Carregar(atualizada, msg);

            ListaMudou?.Invoke();
        }
        catch (Exception ex) { App.LogErro(ex); }
    }

    private void BtnConcluir_Click(object sender, RoutedEventArgs e)
    {
        MarcarCaixaDeConclusao(!CaixaMarcada);
        DesarmarExclusao();
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        DesarmarExclusao();
        GravarSeMudou();
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        Fechar();
        Owner?.Activate();
    }
}
