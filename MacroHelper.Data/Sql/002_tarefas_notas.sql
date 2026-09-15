-- 002_tarefas_notas.sql — as duas telas novas.
--
-- Valem as mesmas convenções de 001: datas em TEXT 'yyyy-MM-dd HH:mm:ss' hora local,
-- booleanos INTEGER 0/1 com CHECK, AUTOINCREMENT em toda PK.

-- ── tarefas ──────────────────────────────────────────────────────────────────
CREATE TABLE tarefas (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo             TEXT    NOT NULL,
    observacoes        TEXT,
    concluida          INTEGER NOT NULL DEFAULT 0 CHECK (concluida IN (0, 1)),

    -- INTEGER e não TEXT: 0 baixa, 1 normal, 2 alta. Como texto, ORDER BY colocaria
    -- "Alta" antes de "Baixa" antes de "Normal" — ordem alfabética, não de urgência.
    prioridade         INTEGER NOT NULL DEFAULT 1 CHECK (prioridade IN (0, 1, 2)),

    -- prazo e lembrete são INDEPENDENTES de propósito: prazo é informativo ("é para
    -- quinta"), lembrete é o instante exato em que a notificação deve aparecer. Querer
    -- ser avisado na quarta de manhã sobre algo que vence na quinta é o caso normal.
    prazo              TEXT,
    lembrete           TEXT,
    lembrete_disparado INTEGER NOT NULL DEFAULT 0 CHECK (lembrete_disparado IN (0, 1)),

    data_criacao       TEXT    NOT NULL,
    data_conclusao     TEXT
);

-- Índice PARCIAL: a varredura do LembreteService roda a cada 30 segundos e só precisa
-- enxergar lembretes pendentes. Com o filtro no índice, o custo é proporcional ao número
-- de lembretes por disparar — não ao número de tarefas já concluídas acumuladas.
CREATE INDEX ix_tarefas_lembrete_pendente ON tarefas(lembrete)
    WHERE lembrete IS NOT NULL AND lembrete_disparado = 0 AND concluida = 0;

CREATE INDEX ix_tarefas_lista ON tarefas(concluida, prazo);

-- ── notas ────────────────────────────────────────────────────────────────────
CREATE TABLE notas (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo           TEXT    NOT NULL,
    conteudo         TEXT    NOT NULL DEFAULT '',
    fixada           INTEGER NOT NULL DEFAULT 0 CHECK (fixada IN (0, 1)),

    -- titulo + conteudo em minúsculas e SEM ACENTOS, gravado pelo repositório na mesma
    -- instrução que grava as outras colunas — nunca num passo separado, que é como esse
    -- tipo de coluna fica para trás e a busca deixa de achar a nota recém-editada.
    --
    -- Existe porque o LIKE do SQLite só ignora maiúsculas para ASCII: sem ela, procurar
    -- "acao" jamais acharia "Ação", e procurar "AÇÃO" também não.
    busca            TEXT    NOT NULL DEFAULT '',

    data_criacao     TEXT    NOT NULL,
    data_atualizacao TEXT
);

CREATE INDEX ix_notas_ordem ON notas(fixada DESC, data_atualizacao DESC);
