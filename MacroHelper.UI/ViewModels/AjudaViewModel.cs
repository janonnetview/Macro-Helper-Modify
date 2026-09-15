using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Reflection;

namespace MacroHelper.UI.ViewModels;

public partial class AjudaViewModel : ObservableObject
{
    public ObservableCollection<FaqItem> Faqs { get; } = new(
    [
        new("Como crio uma macro?",
            "Em Macros, clique em Nova Macro. Defina um atalho em minúsculas com hífens (ex: chamado-recebido), o título e o conteúdo. Depois de salvar, digite o atalho em qualquer campo do Windows.",
            aberta: true),
        new("O que são variáveis e como uso {nome}?",
            "Insira {nome_da_variavel} no conteúdo da macro. Ao usar a macro, o MacroHelper pede o valor se a variável não existir em Variáveis Globais. Use Variáveis Globais para valores fixos como {empresa} ou {assinatura}."),
        new("Dá para calcular datas dentro da macro?",
            "Dá. {data} é hoje; {data+2} é daqui a dois dias; {data-1} foi ontem. Para prazo comercial existe {data+3u}, que são três dias ÚTEIS, pulando sábado e domingo. Também valem {data+2s} (semanas), {mes+1} (mês que vem), {hora+2} e {hora+90m}. Para escolher o formato, use dois-pontos: {data:dd/MM} ou {data+1:dddd}."),
        new("Como faço a macro perguntar entre opções fixas?",
            "Separe as opções por barra vertical: {status:Aberto|Em análise|Concluído}. Na hora de inserir, o campo vira uma lista para escolher em vez de uma caixa de texto, que é por onde entram os \"Em analise\" sem acento num texto que vai para o cliente. Sem barra, o que vem depois dos dois-pontos é o valor que já vem preenchido: {cliente:Fulano}."),
        new("Como deixo o cursor no meio do texto depois de inserir?",
            "Escreva {|} no ponto onde o cursor deve parar. Por exemplo: \"Prezado {|}, segue em anexo.\". O marcador não aparece no texto inserido; ele só posiciona o cursor. {cursor} faz a mesma coisa, para quem prefere escrever por extenso. Se houver mais de um, vale o primeiro."),
        new("O que é a aba Copiados do Ctrl+Espaço?",
            "É o histórico da área de transferência: os últimos 50 textos que você copiou em qualquer programa, buscáveis. Enter insere o item escolhido no programa onde você estava, sem mexer no que está copiado agora. O ícone de alfinete fixa um item no topo (fixado nunca é descartado pelo limite), Shift+Delete apaga um, e o botão Limpar esvazia o resto. Tudo fica no seu computador, e o que um gerenciador de senhas copia não é guardado. Para desligar a captura: Configurações › Gatilho."),
        new("Como faço uma tarefa se repetir toda semana?",
            "No formulário da tarefa, escolha em Repetir: todo dia, dias úteis, toda semana, a cada 15 dias, todo mês ou todo ano. Ao concluir, a próxima ocorrência é criada sozinha, com prazo e lembrete deslocados juntos, e a concluída fica no histórico. Se você concluir com atraso, a próxima cai no futuro, e não numa data que já passou."),
        new("O lembrete tocou numa hora ruim. Dá para adiar?",
            "Dá. Clique no balão do lembrete: a tarefa abre numa janelinha com os botões Adiar para 15 min, 1 hora ou Amanhã (9h). O adiamento rearma a notificação, então ela volta a aparecer no novo horário."),
        new("Qual a diferença entre uma tarefa e um lembrete?",
            "Tarefa é algo A FAZER: pode não ter data nenhuma, e o prazo dela é só informativo. Lembrete tem dia e HORA marcados: a visita do técnico na quinta às 14h, a entrega do documento ao gestor na quarta às 10h. Ele não se conclui: ele acontece, com ou sem você. Por isso a tela de Lembretes é ordenada pelo relógio e o app avisa ANTES, na antecedência que você escolher. Os dois aparecem no Ctrl+Espaço, cada um na sua aba."),
        new("Como funcionam os dois avisos de um lembrete?",
            "Cada lembrete tem até dois. O de folga (1 dia antes, 2 dias, 1 semana) é o que dá tempo de se organizar; o de cima da hora (na hora, 10 ou 30 min, 1 ou 2 horas) é o que faz você levantar da cadeira. Um lembrete novo já vem com 1 dia antes e 30 min antes. Os avisos valem mesmo com o app fechado: ao abrir, o que venceu aparece, e o balão diz quanto FALTA (\"em 25 min\"). Mudar o horário rearma os dois sozinho, e cancelar cala os avisos sem apagar o registro."),
        new("Dá para ver os lembretes num calendário?",
            "Dá. No alto da tela de Lembretes, os botões Lista, Semana e Mês trocam a forma de ver, e a escolha fica gravada para a próxima vez. A grade mostra o que a lista não mostrava: dois compromissos no mesmo horário aparecem lado a lado, em âmbar, e o dia ganha um sinal de aviso. As setas andam um período e Hoje volta para o presente. Na semana, clicar num horário vazio abre o formulário já naquele dia e hora; no mês, clicar no dia abre às 9h. Clicar num compromisso abre ele para editar. Lembrete sem data não cabe na grade, então uma tarja conta quantos são e leva de volta para a lista."),
        new("Dá para usar negrito e listas nas notas?",
            "Dá. O editor de notas entende Markdown e tem dois modos: Escrever mostra o texto com a marcação, Ver mostra a nota desenhada. Escreva # para título, - para lista, 1. para lista numerada, - [ ] para caixa de marcar, **negrito**, *itálico* e `código`. Também valem citação com >, link no formato [texto](endereço) e divisor com ---. Na visualização, clicar numa caixa de marcar escreve a marca no texto da nota, e ela vale depois de salvar. A barra de botões acima do editor escreve a marcação por você, inclusive sobre várias linhas de uma vez."),
        new("Por que o atalho não está funcionando em um programa?",
            "Alguns apps bloqueiam teclas de sistema (ex: navegadores com foco em campo de busca). Tente clicar no campo de texto antes de digitar o atalho, ou use Ctrl+Espaço para abrir o buscador manual."),
        new("Posso recuperar uma versão anterior de uma macro?",
            "Sim, enquanto a macro existir. Abra a macro em Macros e clique no ícone de histórico, no alto do formulário: ele lista o conteúdo antes de cada edição e restaura o que você escolher. Excluir a macro apaga o histórico dela junto; para guardar uma cópia antes, use Exportar em Macros."),
        new("Quando o lembrete de uma tarefa aparece?",
            "Na hora marcada, como notificação do Windows, desde que o app esteja aberto (pode estar minimizado na bandeja). Se ele estava fechado, o lembrete aparece assim que você abrir, dizendo o horário original. Prazo e lembrete são coisas diferentes: o prazo é só informativo, quem avisa é o lembrete."),
        new("Marquei um lembrete e nada apareceu.",
            "Vá em Configurações › Comportamento e clique em \"Testar notificação\". Se o teste também não mostrar nada, as notificações do SK MacroHelper estão desligadas em Configurações do Windows › Sistema › Notificações, e o Windows não avisa o app quando as bloqueia."),
        new("Onde ficam os meus dados?",
            "Num único arquivo em %AppData%\\MacroHelper\\macrohelper.db, no seu computador. Não há servidor, conta nem sincronização: para fazer backup, feche o app e copie esse arquivo."),
    ]);

    public string VersaoApp =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public string SubtituloComVersao => $"MacroHelper {VersaoApp} · atualizado em {DateTime.Today:dd/MM/yyyy}";
}

public partial class FaqItem : ObservableObject
{
    public string Pergunta { get; }
    public string Resposta { get; }
    [ObservableProperty] private bool _aberta;

    public FaqItem(string pergunta, string resposta, bool aberta = false)
    {
        Pergunta = pergunta;
        Resposta = resposta;
        Aberta   = aberta;
    }

    [RelayCommand]
    private void ToggleAberta() => Aberta = !Aberta;
}
