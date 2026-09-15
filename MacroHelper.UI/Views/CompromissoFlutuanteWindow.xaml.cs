using MacroHelper.Core.Entities;
using MacroHelper.Services;
using MacroHelper.UI.Helpers;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MacroHelper.UI.Views;

/// <summary>
/// O lembrete aberto numa janela ao lado da busca rápida — irmã da
/// <see cref="TarefaFlutuanteWindow"/> e com as mesmas regras: grava sozinha ao sair, troca de
/// item sem fechar, e só grava se alguma coisa mudou de verdade.
///
/// (No código isto continua "compromisso"; na tela, "lembrete". A razão está no comentário
/// grande de Compromisso.cs.)
///
/// Existe porque sem ela a aba de Lembretes do Ctrl+Espaço só sabia LISTAR: criar, editar ou
/// apagar obrigava a abrir o app e atravessar até a tela — que é exatamente o trajeto que o
/// atalho existe para evitar. Tarefas e Notas já tinham a delas.
/// </summary>
public partial class CompromissoFlutuanteWindow : Window
{
    private readonly CompromissoService _svc;
    private readonly IRelogio           _relogio;

    private Compromisso? _lembrete;
    private Assinatura   _assinaturaOriginal;
    private bool         _confirmandoExclusao;

    /// <summary>
    /// Tudo que a pessoa pode ter mexido — é o que diz se houve edição de verdade.
    ///
    /// Mesma escolha da janela de tarefa: um record, e não texto concatenado. Sem separador
    /// não há campo capaz de imitar outro, e a igualdade estrutural vem de graça.
    /// </summary>
    private readonly record struct Assinatura(
        string Titulo, string Local, string Observacoes,
        string Data, string Hora, int? AvisoFolga, int? AvisoHora, bool Feito);

    /// <summary>Gravou ou apagou — a lista da busca rápida precisa se refazer nos dois casos.</summary>
    public event Action? ListaMudou;

    public CompromissoFlutuanteWindow(CompromissoService svc, IRelogio relogio)
    {
        InitializeComponent();

        // Sem sombra nem fio do sistema: aqui o limite da janela é a borda do cartão.
        MolduraNativa.Aplicar(this, comSombra: false);

        _svc     = svc;
        _relogio = relogio;

        Deactivated += (_, _) => GravarSeMudou();
    }

    public Compromisso? LembreteAtual => _lembrete;

    /// <summary>Abre um lembrete existente. Chamada de novo com outro, grava o anterior e troca.</summary>
    public void Abrir(Compromisso lembrete)
    {
        if (_lembrete != null && !ReferenceEquals(_lembrete, lembrete)) GravarSeMudou();
        Carregar(lembrete, "Salvo sozinho ao fechar");
        MostrarSePreciso(focoNoTitulo: false);
    }

    /// <summary>
    /// Abre um lembrete em branco. Sem título escrito, nada é gravado ao fechar.
    ///
    /// Já vem com amanhã às 9h e os dois avisos padrão, como o formulário da tela: um
    /// lembrete quase nunca é para daqui a cinco minutos, e campo de data em branco obriga a
    /// digitar o óbvio toda vez.
    /// </summary>
    public void Nova()
    {
        GravarSeMudou();

        Carregar(new Compromisso
        {
            Quando                 = _relogio.Agora.Date.AddDays(1).AddHours(9),
            AvisoAntecipadoMinutos = Antecedencia.PadraoAntecipado,
            AvisoNaHoraMinutos     = Antecedencia.PadraoNaHora,
        }, "Escreva do que é o lembrete");

        MostrarSePreciso(focoNoTitulo: true);
    }

    private void Carregar(Compromisso lembrete, string estado)
    {
        _lembrete = lembrete;

        TxtTitulo.Text      = lembrete.Titulo;
        TxtLocal.Text       = lembrete.Local ?? string.Empty;
        TxtObservacoes.Text = lembrete.Observacoes ?? string.Empty;
        CampoData.Texto     = lembrete.Quando?.ToString("dd/MM/yyyy") ?? string.Empty;
        TxtHora.Text        = lembrete.Quando?.ToString("HH:mm")      ?? string.Empty;

        SelecionarMinutos(CmbAvisoFolga, lembrete.AvisoAntecipadoMinutos);
        SelecionarMinutos(CmbAvisoHora,  lembrete.AvisoNaHoraMinutos);

        MarcarCaixaDeFeito(lembrete.Realizado);

        // Lembrete que ainda não existe não tem o que apagar.
        BtnApagar.Visibility = lembrete.Id == 0 ? Visibility.Collapsed : Visibility.Visible;
        DesarmarExclusao();

        Estado.Mostrar(estado);
        _assinaturaOriginal = AssinaturaAtual();
    }

    /// <summary>
    /// O Tag do botão guarda o estado como TEXTO, e não como bool: o DataTrigger do template
    /// compara com a string "True", e num Tag do tipo object não há conversão para acontecer.
    /// </summary>
    private void MarcarCaixaDeFeito(bool feito) => BtnFeito.Tag = feito ? "True" : "False";

    private bool CaixaMarcada => (string?)BtnFeito.Tag == "True";

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

    /// <summary>Esconde gravando. Hide e não Close: a janela é reaproveitada no próximo lembrete.</summary>
    public void Fechar()
    {
        GravarSeMudou();
        _lembrete = null;
        Hide();
    }

    // ── Os dois combos de aviso ──────────────────────────────────────────────

    /// <summary>Lê os minutos pelo Tag do item escolhido. Tag vazio = este aviso não existe.</summary>
    private static int? MinutosEscolhidos(System.Windows.Controls.ComboBox combo) =>
        combo.SelectedItem is ComboBoxItem { Tag: string tag } && tag.Length > 0
        && int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutos)
            ? minutos
            : null;

    /// <summary>
    /// Marca a opção correspondente aos minutos gravados. Uma antecedência que não esteja na
    /// lista — vinda da tela, que oferece as mesmas — cai em "Não avisar" em vez de mentir
    /// mostrando outra: melhor a pessoa ver que precisa reescolher.
    /// </summary>
    private static void SelecionarMinutos(System.Windows.Controls.ComboBox combo, int? minutos)
    {
        var alvo = minutos?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if ((string?)item.Tag != alvo) continue;
            combo.SelectedItem = item;
            return;
        }

        combo.SelectedIndex = 0;
    }

    // ── Gravação ─────────────────────────────────────────────────────────────

    private Assinatura AssinaturaAtual() =>
        new(TxtTitulo.Text, TxtLocal.Text, TxtObservacoes.Text,
            CampoData.Texto, TxtHora.Text,
            MinutosEscolhidos(CmbAvisoFolga), MinutosEscolhidos(CmbAvisoHora), CaixaMarcada);

    private void GravarSeMudou()
    {
        if (_lembrete == null) return;
        if (AssinaturaAtual() == _assinaturaOriginal) return;

        // Título em branco é lembrete abandonado: o novo que ninguém chegou a escrever, ou o
        // existente que o serviço recusaria de qualquer jeito. Sair sem gravar é o certo.
        var titulo = TxtTitulo.Text.Trim();
        if (titulo.Length == 0) return;

        // Dia ou hora ilegíveis RECUSAM a gravação, em vez de manter o valor anterior como faz
        // o prazo de uma tarefa. A diferença é o que a data significa em cada um: no lembrete
        // ela é o próprio item, e gravar calado o horário antigo faria o aviso sair na hora
        // errada — o único defeito que este módulo não pode ter.
        var data = DataDigitada.Interpretar(CampoData.Texto, _relogio.Agora);

        // Campo de hora vazio é recusado ANTES de chegar ao DataDigitada: lá, texto vazio
        // significa "às 9h" — regra certa para o lembrete de uma tarefa, e errada aqui, onde
        // o horário é o item. A máscara do campo apaga o que não for hora enquanto se digita,
        // então "abc" chega vazio e cairia nas 9h de manhã sem ninguém ter escolhido nada.
        var textoHora = TxtHora.Text.Trim();
        var hora      = textoHora.Length == 0 ? null : DataDigitada.InterpretarHora(textoHora);

        if (data == null || hora == null)
        {
            Estado.Falhar("Dia ou hora não entendidos. Não gravei.");
            return;
        }

        var lembrete = _lembrete;

        lembrete.Titulo      = titulo;
        lembrete.Local       = string.IsNullOrWhiteSpace(TxtLocal.Text) ? null : TxtLocal.Text.Trim();
        lembrete.Observacoes = string.IsNullOrWhiteSpace(TxtObservacoes.Text) ? null : TxtObservacoes.Text.Trim();
        lembrete.Quando      = data.Value.Add(hora.Value);

        lembrete.AvisoAntecipadoMinutos = MinutosEscolhidos(CmbAvisoFolga);
        lembrete.AvisoNaHoraMinutos     = MinutosEscolhidos(CmbAvisoHora);

        // Desmarcar a caixa devolve para a agenda só o que estava marcado como feito. Um
        // lembrete CANCELADO continua cancelado: a caixa não é o lugar de desfazer isso, e
        // reabri-lo por engano faria os avisos dele voltarem a sair.
        lembrete.Situacao = CaixaMarcada
            ? SituacaoCompromisso.Realizado
            : lembrete.Realizado ? SituacaoCompromisso.Agendado : lembrete.Situacao;

        _assinaturaOriginal = AssinaturaAtual();
        GravarAsync(lembrete);
    }

    private async void GravarAsync(Compromisso lembrete)
    {
        try
        {
            var (ok, msg, _) = await _svc.SalvarAsync(lembrete);
            if (!ok) { Estado.Falhar(msg); return; }

            if (ReferenceEquals(_lembrete, lembrete))
            {
                // O serviço apara o título; o campo mostra o que ficou gravado de verdade.
                TxtTitulo.Text       = lembrete.Titulo;
                BtnApagar.Visibility = Visibility.Visible;
                _assinaturaOriginal  = AssinaturaAtual();

                // A mensagem do serviço avisa de choque de horário — vale mais que "Salvo às
                // 14:32", que é o que a janela diria sozinha.
                Estado.Mostrar(msg);
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
        if (_lembrete is not { Id: > 0 }) return;

        // Dois cliques, e o botão diz o que o segundo vai fazer. Um clique só é pouco para
        // algo que não vai para lixeira nenhuma.
        if (!_confirmandoExclusao)
        {
            _confirmandoExclusao = true;
            BtnApagar.Content    = "Apagar mesmo?";
            Estado.Mostrar("Clique de novo para apagar.");
            return;
        }

        var alvo = _lembrete;

        // Zerado ANTES de esconder: o Deactivated dispara a gravação, e sem isso o lembrete
        // seria regravado no caminho para ser apagado.
        _lembrete = null;
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
                Estado.Mostrar("Salvo sozinho ao fechar");
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

    private void BtnFeito_Click(object sender, RoutedEventArgs e)
    {
        MarcarCaixaDeFeito(!CaixaMarcada);
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
