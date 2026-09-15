-- 004_compromissos.sql — dia marcado, hora marcada, e um aviso ANTES.
--
-- Na tela isto se chama "Lembretes"; aqui embaixo, compromissos. O nome do código não
-- acompanhou o da tela porque `lembrete` já é outra coluna deste mesmo banco — a de tarefas,
-- que guarda o instante da notificação. Ver o comentário em Compromisso.cs.
--
-- Valem as mesmas convenções de 001, 002 e 003: datas em TEXT 'yyyy-MM-dd HH:mm:ss' hora
-- local, booleanos INTEGER 0/1 com CHECK, AUTOINCREMENT em toda PK.
--
-- ── Por que uma tabela nova, e não mais colunas em tarefas ───────────────────
--
-- Uma tarefa é algo A FAZER: pode não ter data nenhuma, o prazo dela é informativo e ela
-- termina quando alguém a marca como concluída. Um compromisso É um instante — "quinta,
-- 14h, visita do técnico". Ele não se conclui: ele acontece, com ou sem você, e por isso a
-- pergunta que ele responde não é "isto já venceu?" e sim "fui avisado a tempo?".
--
-- Enfiar os dois na mesma tabela custaria caro duas vezes. Metade das colunas ficaria nula
-- em cada linha (tarefa sem hora, compromisso sem prioridade nem recorrência), e a lista
-- misturaria coisas que se leem de formas diferentes: tarefa se lê por urgência, compromisso
-- se lê por relógio. Duas tabelas, duas telas, cada uma com a ordenação que faz sentido.
CREATE TABLE compromissos (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo       TEXT    NOT NULL,

    -- Onde, ou com quem: "sala do gestor", "em casa", "Teams". É a coluna que devolve o
    -- contexto de um compromisso marcado três semanas antes, quando o título sozinho já não
    -- diz de que se tratava.
    local        TEXT,
    observacoes  TEXT,

    -- NOT NULL, ao contrário do prazo das tarefas: compromisso sem hora marcada não é
    -- compromisso. É esta coluna — e não a data de criação — que ordena a agenda inteira.
    quando       TEXT    NOT NULL,

    -- Quanto tempo ele ocupa. Serve para mostrar "14:00 – 15:00" e para avisar de choque de
    -- horário ao salvar. Nulo quando não faz sentido: "entregar o documento" não dura nada.
    duracao_minutos INTEGER,

    -- 0 agendado · 1 realizado · 2 cancelado.
    --
    -- Existe por causa do aviso, não da estatística: cancelar precisa CALAR as notificações
    -- de um compromisso futuro sem apagar a linha. Apagar também calaria, mas levaria junto o
    -- registro de que aquilo esteve marcado — que é justamente o que se procura depois.
    situacao     INTEGER NOT NULL DEFAULT 0 CHECK (situacao IN (0, 1, 2)),

    -- Os dois avisos, em MINUTOS ANTES de `quando` — distância, e não instante absoluto.
    --
    -- Guardar a distância é o que faz mudar o horário do compromisso reposicionar os dois
    -- avisos sozinho: não existe segundo lugar onde a hora esteja escrita, então não existe
    -- como um aviso ficar apontando para o horário antigo.
    --
    -- São DOIS porque um só não cobre o caso real. Para uma visita técnica na quinta é
    -- preciso saber na quarta (para se organizar) E meia hora antes (para estar pronto): um
    -- aviso de véspera sozinho chega cedo demais para agir, e um de 30 minutos sozinho chega
    -- tarde demais para se preparar. Nulo = este aviso não existe.
    aviso_antecipado_minutos   INTEGER,
    aviso_antecipado_disparado INTEGER NOT NULL DEFAULT 0 CHECK (aviso_antecipado_disparado IN (0, 1)),
    aviso_na_hora_minutos      INTEGER,
    aviso_na_hora_disparado    INTEGER NOT NULL DEFAULT 0 CHECK (aviso_na_hora_disparado IN (0, 1)),

    data_criacao TEXT    NOT NULL
);

-- A agenda é lida por situação e ordenada por `quando`, e a varredura de avisos entra pelo
-- mesmo lado (situacao = 0). Um índice serve aos dois.
--
-- Não há índice sobre o INSTANTE do aviso, de propósito: esse instante é `quando` menos um
-- número de minutos guardado noutra coluna, ou seja, uma expressão — precisaria de índice de
-- expressão para render pouco. A varredura já entra filtrada por situacao = 0, e uma agenda
-- pessoal tem dezenas de linhas, não milhões.
CREATE INDEX ix_compromissos_agenda ON compromissos(situacao, quando);
