using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
// WinForms traz os mesmos nomes do WPF; aqui é sempre WPF.
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RichTextBox = System.Windows.Controls.RichTextBox;

namespace MacroHelper.UI.Helpers;

/// <summary>
/// Liga uma caixa de texto formatado ao texto em Markdown da nota, nos dois sentidos.
///
/// Ligada com uma linha no XAML:
/// <code>h:EditorDeMarcacao.Texto="{Binding EditorConteudo}"</code>
///
/// Existe porque o <c>Document</c> do RichTextBox NÃO é propriedade de dependência: não há
/// {Binding} que chegue nele. Então o texto entra por aqui, vira documento, e cada tecla
/// digitada volta a virar texto — que é o que o banco guarda e a busca lê.
///
/// Junto vêm os três comportamentos que fazem a lista parecer lista: o Enter que continua o
/// marcador, o clique que marca a caixinha e o Ctrl+clique que abre o link. Todos moram aqui
/// para que as duas telas de nota tenham os três sem combinar nada entre si.
/// </summary>
public static class EditorDeMarcacao
{
    public static readonly DependencyProperty TextoProperty =
        DependencyProperty.RegisterAttached(
            "Texto", typeof(string), typeof(EditorDeMarcacao),
            new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AoTrocarOTexto));

    public static void SetTexto(DependencyObject alvo, string? valor) => alvo.SetValue(TextoProperty, valor);
    public static string? GetTexto(DependencyObject alvo) => (string?)alvo.GetValue(TextoProperty);

    /// <summary>
    /// Liga só os comportamentos, sem ligar o texto a propriedade nenhuma.
    ///
    /// É o que a nota flutuante usa: lá não existe ViewModel, e o conteúdo entra e sai pelo
    /// código, que já converte com o <see cref="DocumentoMarkdown"/>. O que ela quer daqui é o
    /// Enter que continua a lista, o clique que marca a caixinha e a colagem como texto.
    /// </summary>
    public static readonly DependencyProperty LigadoProperty =
        DependencyProperty.RegisterAttached(
            "Ligado", typeof(bool), typeof(EditorDeMarcacao), new PropertyMetadata(false, AoLigar));

    public static void SetLigado(DependencyObject alvo, bool valor) => alvo.SetValue(LigadoProperty, valor);
    public static bool GetLigado(DependencyObject alvo) => (bool)alvo.GetValue(LigadoProperty);

    private static void AoLigar(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox caixa && e.NewValue is true) Editor(caixa);
    }

    /// <summary>O tratador de uma caixa, guardado nela mesma. Um por caixa, criado no primeiro uso.</summary>
    private static readonly DependencyProperty EditorProperty =
        DependencyProperty.RegisterAttached(
            "Editor", typeof(EditorDeUmaCaixa), typeof(EditorDeMarcacao), new PropertyMetadata(null));

    private static void AoTrocarOTexto(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox caixa) return;

        Editor(caixa).Mostrar((string?)e.NewValue);
    }

    /// <summary>O tratador desta caixa, criado no primeiro uso e guardado nela.</summary>
    private static EditorDeUmaCaixa Editor(RichTextBox caixa)
    {
        if (caixa.GetValue(EditorProperty) is EditorDeUmaCaixa ja) return ja;

        var editor = new EditorDeUmaCaixa(caixa);
        caixa.SetValue(EditorProperty, editor);
        return editor;
    }

    private sealed class EditorDeUmaCaixa
    {
        private readonly RichTextBox _caixa;

        /// <summary>O último texto que passou por aqui, nos dois sentidos. É o que corta o vaivém.</summary>
        private string _ultimo = string.Empty;

        private bool _escrevendo;

        public EditorDeUmaCaixa(RichTextBox caixa)
        {
            _caixa = caixa;

            caixa.TextChanged                += (_, _) => Gravar();
            caixa.PreviewKeyDown             += AoTeclar;
            caixa.PreviewMouseLeftButtonDown += AoClicar;

            // Ctrl+clique num link abre o navegador, como no Word. Sem isto o link é só um
            // texto azul: dentro de um editor, o clique comum serve para pôr o cursor.
            caixa.AddHandler(Hyperlink.RequestNavigateEvent,
                new RequestNavigateEventHandler((_, e) => Navegador.Abrir(e.Uri?.ToString())));

            // Colar entra como TEXTO, sempre. O que vem de um navegador ou do Word carrega
            // tabela, seção e fonte de outro tamanho — coisas que não têm representação no
            // Markdown que esta nota grava, e que voltariam estragadas na próxima abertura.
            DataObject.AddPastingHandler(caixa, AoColar);
        }

        /// <summary>Texto novo vindo de fora (abriu outra nota): vira documento.</summary>
        public void Mostrar(string? markdown)
        {
            markdown ??= string.Empty;

            if (_escrevendo || markdown == _ultimo) return;

            _ultimo = markdown;
            _caixa.Document = DocumentoMarkdown.Montar(markdown);
        }

        /// <summary>Documento mexido: vira texto e sobe para quem estiver ligado na propriedade.</summary>
        private void Gravar()
        {
            var markdown = DocumentoMarkdown.Ler(_caixa.Document);
            if (markdown == _ultimo) return;

            _ultimo     = markdown;
            _escrevendo = true;

            try { SetTexto(_caixa, markdown); }
            finally { _escrevendo = false; }
        }

        private static void AoColar(object remetente, DataObjectPastingEventArgs e)
        {
            if (e.FormatToApply == DataFormats.UnicodeText) return;

            if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
                e.FormatToApply = DataFormats.UnicodeText;
            else
                e.CancelCommand();
        }

        /// <summary>
        /// O Enter dentro de uma lista continua a lista, e o Enter num item vazio sai dela. É o
        /// que todo editor faz, e sem isso cada item pediria um clique no botão da barra.
        /// </summary>
        private void AoTeclar(object remetente, KeyEventArgs e)
        {
            if (e.Key != Key.Return || Keyboard.Modifiers != ModifierKeys.None) return;
            if (_caixa.CaretPosition.Paragraph is not { } p) return;

            var marcador = MarcacaoDeTexto.MarcadorDe(p);
            if (marcador.Length == 0) return;

            var conteudo = new TextRange(p.ContentStart, p.ContentEnd).Text;

            // Item vazio: o Enter tira o marcador em vez de criar outro item vazio embaixo.
            if (conteudo.Trim() == marcador.Trim())
            {
                MarcacaoDeTexto.TirarMarcador(p);
                e.Handled = true;
                return;
            }

            // O Enter acontece normalmente; o marcador entra no parágrafo recém-nascido, depois
            // que o WPF terminou de criá-lo.
            _caixa.Dispatcher.BeginInvoke(() => Continuar(marcador), DispatcherPriority.Background);
        }

        private void Continuar(string marcador)
        {
            if (_caixa.CaretPosition.Paragraph is not { } novo) return;
            if (MarcacaoDeTexto.MarcadorDe(novo).Length > 0) return;

            // Tarefa feita gera tarefa por fazer: o risco e o cinza da linha anterior vêm junto
            // no parágrafo novo, e teriam de sair na mão.
            if (marcador == DocumentoMarkdown.CaixaMarcada)
            {
                marcador = DocumentoMarkdown.CaixaVazia;
                novo.ClearValue(Paragraph.TextDecorationsProperty);
                novo.ClearValue(TextElement.ForegroundProperty);
            }

            MarcacaoDeTexto.PorMarcador(novo, Proximo(marcador));

            if (novo.Inlines.FirstInline is { } primeiro) _caixa.CaretPosition = primeiro.ElementEnd;
        }

        /// <summary>Numa lista numerada, o item seguinte é o número seguinte.</summary>
        private static string Proximo(string marcador)
        {
            var corte = marcador.IndexOf('.');
            if (corte <= 0 || !int.TryParse(marcador[..corte], out var numero)) return marcador;

            return $"{numero + 1}.\t";
        }

        /// <summary>
        /// O clique EM CIMA da caixinha marca e desmarca a tarefa. Só em cima dela: clicar no
        /// texto do item é pôr o cursor ali, como em qualquer outra linha.
        /// </summary>
        private void AoClicar(object remetente, MouseButtonEventArgs e)
        {
            var ponto = e.GetPosition(_caixa);
            var lugar = _caixa.GetPositionFromPoint(ponto, true);

            if (lugar?.Paragraph is not { } p) return;

            var marcador = MarcacaoDeTexto.MarcadorDe(p);
            if (marcador != DocumentoMarkdown.CaixaVazia && marcador != DocumentoMarkdown.CaixaMarcada) return;

            var caixinha = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            if (ponto.X > caixinha.Right + 6) return;

            MarcacaoDeTexto.AlternarTarefa(p);
            e.Handled = true;
        }
    }
}
