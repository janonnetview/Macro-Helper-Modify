-- ═══════════════════════════════════════════════════════════════════════════════
-- 005 — o lembrete pode existir antes de a data existir
-- ═══════════════════════════════════════════════════════════════════════════════
--
-- A 004 dizia, em voz alta, que "compromisso sem hora marcada não é compromisso", e
-- gravava `quando` como NOT NULL. Era uma boa regra e está sendo afrouxada de propósito,
-- por um caso que ela não previa: o compromisso REMARCADO.
--
-- "A visita do técnico saiu de quinta e ainda não tem data nova." Isso não é um
-- compromisso agendado, mas também não é nada — é uma coisa que precisa voltar a ter
-- data, e que some da cabeça de quem não a anotar em lugar nenhum. Com a regra antiga a
-- única saída era inventar uma data falsa, que é pior: data falsa dispara aviso falso.
--
-- O que NÃO muda: sem data não há de onde descontar a antecedência, então um lembrete por
-- marcar não tem aviso nenhum. Quem garante isso é o CompromissoService, que zera as duas
-- colunas de aviso ao gravar sem data. A varredura de avisos também não o alcança sozinha:
-- datetime(NULL, ...) é NULL, e NULL <= @ate não é verdadeiro.
--
-- SQLite não tem ALTER COLUMN. Afrouxar um NOT NULL é recriar a tabela, copiar as linhas e
-- trocar os nomes: daí o tamanho deste arquivo para uma mudança de uma palavra.
--
-- Sem PRAGMA foreign_keys aqui: o migrator roda cada script dentro de uma transação, e esse
-- pragma é ignorado dentro de uma. Escrevê-lo daria a impressão falsa de proteção. Nada
-- aponta para compromissos, então não há o que proteger.

CREATE TABLE compromissos_novo (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo       TEXT    NOT NULL,
    local        TEXT,
    observacoes  TEXT,

    -- A única diferença para a 004: aceita NULL. NULL significa "ainda por marcar", e é
    -- diferente de qualquer data — inclusive de hoje.
    quando       TEXT,

    duracao_minutos INTEGER,
    situacao     INTEGER NOT NULL DEFAULT 0 CHECK (situacao IN (0, 1, 2)),
    aviso_antecipado_minutos   INTEGER,
    aviso_antecipado_disparado INTEGER NOT NULL DEFAULT 0 CHECK (aviso_antecipado_disparado IN (0, 1)),
    aviso_na_hora_minutos      INTEGER,
    aviso_na_hora_disparado    INTEGER NOT NULL DEFAULT 0 CHECK (aviso_na_hora_disparado IN (0, 1)),
    data_criacao TEXT    NOT NULL
);

INSERT INTO compromissos_novo
    (id, titulo, local, observacoes, quando, duracao_minutos, situacao,
     aviso_antecipado_minutos, aviso_antecipado_disparado,
     aviso_na_hora_minutos, aviso_na_hora_disparado, data_criacao)
SELECT
     id, titulo, local, observacoes, quando, duracao_minutos, situacao,
     aviso_antecipado_minutos, aviso_antecipado_disparado,
     aviso_na_hora_minutos, aviso_na_hora_disparado, data_criacao
FROM compromissos;

DROP TABLE compromissos;
ALTER TABLE compromissos_novo RENAME TO compromissos;

-- O índice morreu com a tabela antiga. Mesma definição da 004: a agenda e a varredura de
-- avisos entram as duas por situacao e saem ordenadas por quando.
CREATE INDEX ix_compromissos_agenda ON compromissos(situacao, quando);
