# 🎬 Roteiro de vídeo — Como a arquitetura foi pensada

Um roteiro passo a passo (estilo screencast/voiceover) contando o **raciocínio** por trás da arquitetura do
Restaurant Delivery MVP — não só o resultado, mas as decisões e os porquês.

- **Público:** devs / arquitetos / tech leads.
- **Duração alvo:** ~10–12 min.
- **Tom:** direto, didático, sem jargão desnecessário. Primeira pessoa do plural ("a gente decidiu…").
- **Formato:** narração + gravação de tela (diagramas, código, terminal, demo). Cada cena traz
  `[VISUAL]` (o que mostrar), `🎙️ NARRAÇÃO` (o que falar) e `TELA:` (texto/lower-third na tela).
- **Documentos de apoio para mostrar:** [`README.md`](README.md), [`SYSTEM_DESIGN.md`](SYSTEM_DESIGN.md),
  [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md), os ADRs e o `_techspec.md`.

### 🎥 Capturas para gravar antes (b-roll)
1. Os diagramas C4 do README renderizados (Context, Container, Component) e a sequência runtime.
2. `docker compose up -d --wait` subindo os 14 containers até "healthy".
3. A demo: app Angular nos 3 papéis com a barra de 5 estágios avançando ao vivo.
4. Terminal rodando os testes (E2E chegando a "Delivered").
5. A máquina de estados da saga (`OrderStateMachine.cs`) no editor.

---

## CENA 0 — Hook (0:00–0:25)

`[VISUAL]` Tela dividida: à esquerda o app de delivery funcionando (consumidor pedindo), à direita o
diagrama Container do C4. Corte rápido para o terminal com `docker compose up` finalizando "healthy".

🎙️ **NARRAÇÃO:**
> "Como você desenharia, do zero, um delivery nos moldes do iFood — com consumidor, restaurante e motorista,
> em microsserviços — de um jeito que sobe inteiro com **um comando** e que dá pra trocar cada integração
> externa depois sem reescrever o resto? Neste vídeo eu mostro **como a gente pensou** essa arquitetura,
> decisão por decisão."

`TELA:` **Como pensamos a arquitetura — Restaurant Delivery MVP**

---

## CENA 1 — O problema e a restrição (0:25–1:20)

`[VISUAL]` O `prompt.md` original em destaque (a frase em português pedindo o MVP mockado em microsserviços).
Destacar as 5 áreas: busca, cardápio, pedido, motorista, pagamento.

🎙️ **NARRAÇÃO:**
> "Tudo começou com uma frase: um **MVP mockado** de busca de restaurantes nos moldes do iFood, em
> microsserviços, cobrindo busca, cardápio, pedido, associação de motorista e pagamento. 'Mockado' é a
> palavra‑chave: não é pra processar dinheiro de verdade — é pra **provar a arquitetura** e servir de
> fundação. Isso muda tudo: o produto aqui não é a tela bonita, é a **estrutura**."

`TELA:` Objetivo: PoC + fundação para um produto real (não competir com o iFood)

---

## CENA 2 — As perguntas que moldaram o escopo (1:20–2:30)

`[VISUAL]` Mostrar as decisões de produto como "cards": 3 lados interativos · happy path · **pagamento antes
do motorista** · seletor de papel sem login.

🎙️ **NARRAÇÃO:**
> "Antes de qualquer código, a gente fez perguntas de produto. Quais lados são interativos? Resposta: os
> **três** — consumidor, restaurante e motorista. Qual a profundidade? Um **caminho feliz**, mais **uma**
> falha que importa. E uma decisão sutil que define a saga inteira: o **pagamento acontece antes** de
> procurar motorista. Por quê? Porque você não quer despachar um entregador e o pagamento falhar — e porque
> isso cria, de propósito, o único cenário de falha que vale a pena modelar: pagou, mas não tem motorista."

`TELA:` Decisão‑chave: pagar → depois despachar (gera a compensação "sem motorista → estorno")

---

## CENA 3 — O insight central: os seams trocáveis (2:30–3:40)

`[VISUAL]` Animação simples: caixas "Payment / Maps / Notification" com um conector (porta) e duas
implementações plugáveis (Mock ↔ Real). Mostrar a interface `IPaymentPort` no código.

🎙️ **NARRAÇÃO:**
> "Aqui está o coração do design. Como é um PoC que vira produto, o critério de sucesso não é 'parece o
> iFood' — é: **dá pra trocar um mock por uma integração real sem mexer nos vizinhos?** Então cada
> dependência externa — pagamento, mapas, notificação — vive atrás de uma **porta** (interface), com um
> adapter mock hoje e um real amanhã. E o pagamento é **async‑shaped** desde já: `Capture` devolve
> 'aceito', e o resultado chega depois por callback. Modelar a forma assíncrona agora evita reescrever tudo
> quando o provedor real chegar."

`TELA:` Ports & Adapters — o seam trocável é o verdadeiro produto (ADR‑001)

---

## CENA 4 — O debate: largura x profundidade (3:40–4:50)

`[VISUAL]` Mostrar os "conselheiros" (Pragmatic Engineer, Architect, Product Mind, Devil's Advocate, The
Thinker) como avatares; uma balança "1 fatia profunda ↔ 3 lados completos".

🎙️ **NARRAÇÃO:**
> "Teve tensão de verdade no design. Um conselho de perspectivas debateu: pra **provar** microsserviços,
> melhor uma fatia vertical bem profunda, ou os três lados completos? Os engenheiros puxavam pra
> profundidade — 'largura demonstra superfície, não swappability'. O Devil's Advocate cravou: 'uma saga que
> nunca compensa é uma saga de mentira'. No fim, **quem decide é o dono do produto**: optamos pela
> **largura** (os 3 lados demonstráveis), mas trouxemos as preocupações do conselho como **riscos
> mitigados** — inclusive incluir a compensação no MVP."

`TELA:` Decisões registradas como ADRs (Architecture Decision Records)

---

## CENA 5 — A decomposição em serviços (4:50–6:00)

`[VISUAL]` Diagrama Container do C4 aparecendo serviço a serviço: Gateway → Search, Catalog, Order, Payment,
Dispatch, Tracking, Notification. Cada um "acende" com seu datastore.

🎙️ **NARRAÇÃO:**
> "A decomposição segue a referência da indústria (DoorDash, Uber Eats): um **gateway/BFF** como entrada
> única, e sete serviços com fronteiras claras — **Search** (descoberta), **Catalog** (cardápios), **Order**
> (o núcleo, com a saga), **Payment**, **Dispatch** (motorista), **Tracking** (status) e **Notification**.
> Regra de ouro: **nenhum serviço lê o banco do outro**. Dados cruzam só por **eventos**. Isso é o que
> mantém as fronteiras honestas."

`TELA:` 7 serviços + Gateway · dados cruzam só por eventos (ADR‑004, ADR‑005)

---

## CENA 6 — Comunicação assíncrona e a saga (6:00–7:30)

`[VISUAL]` A máquina de estados (`OrderStateMachine.cs`) no editor + um diagrama dos estados:
`Placed → AwaitingPayment → Paid → Preparing → AwaitingDriver → DriverAssigned → PickedUp → Delivered`,
com o ramo `DriverUnavailable → RefundPayment → NoDriverRefunded`.

🎙️ **NARRAÇÃO:**
> "Os serviços conversam de forma **assíncrona** via RabbitMQ. E o pedido é coordenado por uma **saga
> orquestrada** dentro do Order: um coordenador central, não coreografia — porque a gente queria o ciclo de
> vida **visível** e uma compensação clara. O pedido entra, a saga manda capturar o pagamento, espera o
> 'settled', o restaurante aceita e marca pronto, o sistema pede um motorista, ele retira e entrega. E se
> **não houver motorista**? A saga estorna e fecha num estado terminal consistente — nada de 'pago e não
> entregue'. Tudo com **outbox transacional** (salva e publica de forma atômica) e **idempotência** por
> `(pedido, correlação)`."

`TELA:` Saga orquestrada + outbox transacional + idempotência (ADR‑004)

---

## CENA 7 — Persistência polyglot (7:30–8:15)

`[VISUAL]` Cada serviço com seu banco: Order/Payment → PostgreSQL, Catalog → MongoDB, Search →
Elasticsearch, Dispatch/Tracking → Redis.

🎙️ **NARRAÇÃO:**
> "Cada serviço escolhe o banco que faz sentido pro seu padrão de acesso: **PostgreSQL** pro pedido e
> pagamento (transacional, com a saga e o outbox), **MongoDB** pro catálogo (documentos), **Elasticsearch**
> pra busca, **Redis** pra dispatch e tracking. É a persistência **polyglot** demonstrando autonomia de
> verdade — cada um dono dos seus dados."

`TELA:` Polyglot persistence — o banco certo para cada serviço (ADR‑006)

---

## CENA 8 — Tempo real: "um pedido, três telas" (8:15–9:10)

`[VISUAL]` Demo: três janelas do app (consumidor, restaurante, motorista). Uma ação no restaurante acende o
estágio na barra do consumidor **ao vivo**. Mostrar o hub SignalR.

🎙️ **NARRAÇÃO:**
> "A credibilidade da demo está em **um pedido refletido nas três telas ao vivo**. O gateway hospeda um hub
> **SignalR**: ele consome os eventos do pedido e empurra o `OrderStatusChanged` pra barra de 5 estágios do
> consumidor — em menos de dois segundos depois da ação do restaurante ou do motorista. Quando reconecta, o
> cliente ressincroniza pelo Tracking. É aí que microsserviço deixa de ser diagrama e vira experiência."

`TELA:` SignalR + barra de 5 estágios (ADR‑007)

---

## CENA 9 — O que deu errado (e por que isso é bom) (9:10–10:40)

`[VISUAL]` Terminal rodando o teste **E2E**; destacar o momento em que ele expõe o bug do outbox. Depois, o
erro de **licença do MassTransit 9** no `docker compose up`, e a troca para o MassTransit 8.

🎙️ **NARRAÇÃO:**
> "Arquitetura boa não é a que não tem bug — é a que **revela** os bugs cedo. O teste **end‑to‑end**, subindo
> os serviços contra um RabbitMQ real, pegou um defeito que os testes por serviço escondiam: os endpoints de
> aceitar/entregar publicavam no outbox, mas **não davam commit** — em produção, o evento nunca saía.
> Corrigimos o flush. Depois, ao subir o stack completo, descobrimos que o **MassTransit 9** virou
> **comercial** — o broker não inicia sem licença. Trocamos pro **MassTransit 8 (Apache‑2.0)**: zero mudança
> no código dos serviços, e o stack passou a subir **sem licença nenhuma**. Esse é o valor de testar de
> verdade e de manter os seams trocáveis."

`TELA:` O E2E achou um bug real · MassTransit 9 (comercial) → 8 (Apache‑2.0)

---

## CENA 10 — O resultado: um comando (10:40–11:30)

`[VISUAL]` `docker compose up -d --wait` → 14 containers "healthy". Em seguida, o curl/da demo: `place →
settle → accept → ready → pickup → deliver` → **Tracking stage 5: Delivered**.

🎙️ **NARRAÇÃO:**
> "O resultado: **`docker compose up`** sobe os 14 containers — cinco de infra, sete serviços, gateway e o
> app — e um pedido percorre o fluxo inteiro, pelo gateway, até **Entregue**. Tudo testado: cerca de 300
> testes, com Testcontainers e um E2E full‑stack. E **sem licença comercial**. A fundação está de pé."

`TELA:` 1 comando · ~300 testes · cobertura 90–100% · license‑free

---

## CENA 11 — Fechamento (11:30–12:00)

`[VISUAL]` Voltar aos documentos: README com os C4, `SYSTEM_DESIGN.md`, os ADRs. Card final com o repositório.

🎙️ **NARRAÇÃO:**
> "Resumindo o raciocínio: comece pelo **porquê** (provar e fundar, não competir), transforme isso num
> **critério** (seams trocáveis), deixe o **produto** decidir largura x profundidade, modele o ciclo de vida
> numa **saga visível**, e **prove com testes reais** — eles vão te pagar de volta em bugs achados cedo.
> Tudo isso está documentado nos ADRs e no System Design do repositório. Valeu, e até a próxima."

`TELA:` Decisões → ADRs · Arquitetura → SYSTEM_DESIGN.md · obrigado!

---

## 📋 Apêndice — checklist de gravação
- [ ] Narração gravada por cena (ou contínua, com marcações de tempo acima)
- [ ] B‑roll 1–5 capturado (diagramas, compose up, demo 3 telas, testes, saga no editor)
- [ ] Lower‑thirds (`TELA:`) inseridos em cada cena
- [ ] Trilha de fundo suave; volume da narração normalizado
- [ ] Legendas (PT) — bom para acessibilidade e alcance
- [ ] Cartela final com o link do repositório

## 🎙️ Apêndice — narração corrida (para teleprompter)
Para gravar de uma vez, leia em sequência os blocos `🎙️ NARRAÇÃO` das Cenas 0 a 11. Tempo estimado de fala:
~10–12 minutos em ritmo natural.
