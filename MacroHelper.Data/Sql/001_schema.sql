-- 001_schema.sql — esquema inicial do MacroHelper local (SQLite).
--
-- Este arquivo é a fonte da verdade do banco. Nenhuma tabela é criada em código C#.
--
-- Convenções válidas para esta e para todas as migrações seguintes:
--
--   • Datas são TEXT no formato 'yyyy-MM-dd HH:mm:ss', em HORA LOCAL. Nesse formato o
--     texto ordena na mesma ordem que o tempo, então BETWEEN e ORDER BY usam o índice
--     direto. DataSqliteHandler fixa o formato na ida e na volta.
--
--   • Booleanos são INTEGER 0/1 com CHECK — SQLite não tem tipo booleano, e sem o CHECK
--     um valor 2 gravado por engano viraria "true" silenciosamente.
--
--   • Toda PK usa AUTOINCREMENT. Sem ele o SQLite reaproveita o id de uma linha excluída;
--     um log_uso órfão passaria a apontar para a macro errada em vez de para nada.

-- ── categorias ───────────────────────────────────────────────────────────────
CREATE TABLE categorias (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    nome         TEXT    NOT NULL COLLATE NOCASE,
    icone        TEXT,
    cor          TEXT,
    -- ON DELETE SET NULL substitui o laço manual que o CategoriaRepository fazia ao
    -- excluir uma categoria: buscar as filhas, zerar o pai de cada uma, gravar N vezes.
    pai_id       INTEGER REFERENCES categorias(id) ON DELETE SET NULL,
    ordem        INTEGER NOT NULL DEFAULT 0,
    data_criacao TEXT    NOT NULL
);

-- Dois índices parciais em vez de um UNIQUE(nome, pai_id): num índice único do SQLite
-- dois NULL são DISTINTOS entre si, então "Jurídico" raiz poderia ser cadastrado duas
-- vezes sem violar nada.
CREATE UNIQUE INDEX ux_categorias_raiz  ON categorias(nome) WHERE pai_id IS NULL;
CREATE UNIQUE INDEX ux_categorias_filha ON categorias(nome, pai_id) WHERE pai_id IS NOT NULL;

-- ── macros ───────────────────────────────────────────────────────────────────
CREATE TABLE macros (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    atalho           TEXT    NOT NULL COLLATE NOCASE,
    titulo           TEXT    NOT NULL,
    conteudo         TEXT    NOT NULL,
    -- categoria (texto) fica desnormalizado de propósito: é lido no caminho do hook de
    -- teclado e no filtro da listagem, onde um JOIN por tecla digitada não se paga.
    -- CategoriaRepository.UpdateAsync mantém a sincronia ao renomear a categoria.
    categoria        TEXT,
    categoria_id     INTEGER REFERENCES categorias(id) ON DELETE SET NULL,
    ativo            INTEGER NOT NULL DEFAULT 1 CHECK (ativo    IN (0, 1)),
    favorito         INTEGER NOT NULL DEFAULT 0 CHECK (favorito IN (0, 1)),
    atalho_tecla     TEXT,
    imagem_base64    TEXT,
    data_criacao     TEXT    NOT NULL,
    data_atualizacao TEXT
);

-- A coluna é COLLATE NOCASE, então o índice também é: duas macros "/ola" e "/OLA" passam
-- a colidir aqui, no banco, em vez de gerar duas macros que o C# (OrdinalIgnoreCase)
-- trata como a mesma.
CREATE UNIQUE INDEX ux_macros_atalho ON macros(atalho);

-- Parcial porque a maioria das macros não tem atalho de tecla, e NULL não deve colidir
-- com NULL. Ctrl+Alt+N precisa resolver para exatamente uma macro.
CREATE UNIQUE INDEX ux_macros_atalho_tecla ON macros(atalho_tecla) WHERE atalho_tecla IS NOT NULL;

CREATE INDEX ix_macros_categoria_id ON macros(categoria_id);
CREATE INDEX ix_macros_ativo_titulo ON macros(ativo, titulo);

-- ── variaveis_globais ────────────────────────────────────────────────────────
CREATE TABLE variaveis_globais (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    nome         TEXT    NOT NULL COLLATE NOCASE,
    valor_padrao TEXT    NOT NULL DEFAULT '',
    descricao    TEXT,
    data_criacao TEXT    NOT NULL
);

-- Obrigatório, não cosmético: VariavelGlobalService monta um dicionário por nome com
-- StringComparer.OrdinalIgnoreCase. Dois registros com o mesmo nome derrubariam a
-- resolução de variáveis com ArgumentException na hora de inserir a macro.
CREATE UNIQUE INDEX ux_variaveis_globais_nome ON variaveis_globais(nome);

-- ── macro_versoes ────────────────────────────────────────────────────────────
CREATE TABLE macro_versoes (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    -- CASCADE aqui (e SET NULL no log): histórico de versões de uma macro que não existe
    -- mais não tem leitor nem sentido.
    macro_id         INTEGER NOT NULL REFERENCES macros(id) ON DELETE CASCADE,
    titulo           TEXT    NOT NULL,
    conteudo         TEXT    NOT NULL,
    data_modificacao TEXT    NOT NULL
);

CREATE INDEX ix_macro_versoes_macro ON macro_versoes(macro_id, data_modificacao DESC);

-- ── log_uso ──────────────────────────────────────────────────────────────────
CREATE TABLE log_uso (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    -- SET NULL, nunca CASCADE: o histórico de uso deve sobreviver à exclusão da macro.
    -- É exatamente por isso que titulo e atalho estão copiados aqui.
    macro_id     INTEGER REFERENCES macros(id) ON DELETE SET NULL,
    macro_titulo TEXT    NOT NULL,
    macro_atalho TEXT    NOT NULL,
    aplicativo   TEXT,
    data_uso     TEXT    NOT NULL,
    caracteres   INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_log_uso_data ON log_uso(data_uso DESC);
