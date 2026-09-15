-- 003_clipboard_e_recorrencia.sql — histórico da área de transferência e tarefas que voltam.
--
-- Valem as mesmas convenções de 001 e 002: datas em TEXT 'yyyy-MM-dd HH:mm:ss' hora local,
-- booleanos INTEGER 0/1 com CHECK, AUTOINCREMENT em toda PK.

-- ── clipboard ────────────────────────────────────────────────────────────────
--
-- O que passou pela área de transferência, para poder ser colado de novo depois de já ter
-- sido sobrescrito. É o mesmo problema que o Win+V do Windows resolve, com duas diferenças
-- que importam aqui: o texto entra no app de destino pelo mesmo caminho de uma macro (sem
-- passar pelo clipboard de novo, sem roubar o que estiver nele agora), e o que está fixado
-- fica ao lado das macros na mesma janela de busca.
CREATE TABLE clipboard (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    conteudo   TEXT    NOT NULL,

    -- Fixado nunca é podado pela retenção — é a diferença entre "copiei há pouco" e "isto eu
    -- uso todo dia". Mesma ideia do fixada das notas.
    fixado     INTEGER NOT NULL DEFAULT 0 CHECK (fixado IN (0, 1)),

    -- Título da janela de onde o texto veio ("Chamado 4412 — Chrome"). É o que faz uma lista
    -- de trechos parecidos voltar a ser distinguível três horas depois.
    origem     TEXT,

    -- Minúsculo e sem acentos, gravado na mesma instrução que grava o conteúdo — pela mesma
    -- razão da coluna busca das notas: o LIKE do SQLite só ignora maiúsculas para ASCII.
    busca      TEXT    NOT NULL DEFAULT '',

    data_copia TEXT    NOT NULL
);

-- Copiar de novo um texto que já está no histórico não cria linha nova: o repositório acha
-- por este índice e sobe a data, como fazem o Win+V e o Ditto. O índice é o que garante que
-- não exista caminho — nem uma corrida entre duas cópias rápidas — capaz de duplicar.
-- Sem COLLATE NOCASE de propósito: "OK" e "ok" são dois textos diferentes para colar.
CREATE UNIQUE INDEX ux_clipboard_conteudo ON clipboard(conteudo);

CREATE INDEX ix_clipboard_ordem ON clipboard(fixado DESC, data_copia DESC);

-- ── tarefas.recorrencia ──────────────────────────────────────────────────────
--
-- 0 nenhuma · 1 diária · 2 dias úteis · 3 semanal · 4 quinzenal · 5 mensal · 6 anual.
--
-- INTEGER e não TEXT pela mesma razão de prioridade: como texto, "Anual" ordenaria antes de
-- "Diária". Aqui a ordem é a do intervalo, do mais curto para o mais longo.
--
-- Concluir uma tarefa recorrente CRIA a próxima ocorrência e passa a recorrência para ela —
-- a concluída fica no histórico como tarefa comum. Ver TarefaService.ConcluirAsync: é o que
-- impede que reabrir uma tarefa já concluída gere uma segunda cópia da mesma ocorrência.
ALTER TABLE tarefas ADD COLUMN recorrencia INTEGER NOT NULL DEFAULT 0
    CHECK (recorrencia IN (0, 1, 2, 3, 4, 5, 6));
