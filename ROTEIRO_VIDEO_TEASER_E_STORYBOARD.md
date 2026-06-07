# 🎬 Teaser (60–90s) + Storyboard — Como a arquitetura foi pensada

Complemento do roteiro completo ([`ROTEIRO_VIDEO_ARQUITETURA.md`](ROTEIRO_VIDEO_ARQUITETURA.md)). Aqui tem
**duas coisas**:
1. **Teaser** — um corte enxuto de ~60–90s (hook + 3 ideias + resultado).
2. **Storyboard** — o vídeo completo painel a painel, com frames em ASCII, tipo de plano, texto na tela,
   áudio e transição.

Convenções: `PLANO` = enquadramento/tipo de tomada · `DUR` = duração · `TELA` = texto/lower-third na tela ·
`ÁUDIO` = narração/efeito · `→` = transição.

---

# PARTE A — Teaser (60–90s)

Ritmo rápido, cortes secos, música crescente. Total ~75s.

| # | DUR | PLANO | TELA | 🎙️ ÁUDIO (narração) |
|---|-----|-------|------|---------------------|
| T1 | 0–6s | App rodando (consumidor) + corte pro diagrama C4 | **Como pensar um delivery em microsserviços** | "Um delivery nos moldes do iFood. Microsserviços. Do zero." |
| T2 | 6–16s | Zoom no `prompt.md` → 5 ícones (busca, cardápio, pedido, motorista, pagamento) | Mockado = provar a arquitetura | "O pedido era um MVP **mockado**. Ou seja: o produto não é a tela — é a **arquitetura**." |
| T3 | 16–30s | Animação ports/adapters: Mock ↔ Real plugando | Ideia 1 · Seams trocáveis | "Ideia um: tudo que é externo — pagamento, mapas, notificação — atrás de uma **porta**. Troca o mock pelo real **sem tocar nos vizinhos**." |
| T4 | 30–44s | Máquina de estados da saga animando os estados | Ideia 2 · Saga orquestrada | "Ideia dois: o pedido vira uma **saga** — visível, com compensação. Pagou e não tem motorista? **Estorna** e fecha limpo." |
| T5 | 44–56s | Demo 3 telas: ação no restaurante acende a barra do consumidor | Ideia 3 · Um pedido, três telas ao vivo | "Ideia três: **tempo real**. Uma ação acende as três telas ao vivo, via SignalR." |
| T6 | 56–70s | `docker compose up` → "healthy" + curl chegando a "Delivered" | 1 comando · ~300 testes · sem licença | "Resultado: **um comando** sobe os 14 containers e o pedido vai de ponta a ponta. Testado. E **sem licença comercial**." |
| T7 | 70–75s | Cartela final com repositório | github.com/otimize-io/test-compozy-restaurant-delivery | "Como? Tá tudo no repositório. Bora?" |

**Narração corrida do teaser (teleprompter):**
> "Um delivery nos moldes do iFood, em microsserviços, do zero. O pedido era um MVP **mockado** — então o
> produto não é a tela, é a **arquitetura**. Ideia um: tudo que é externo fica atrás de uma **porta** — troca
> o mock pelo real sem tocar nos vizinhos. Ideia dois: o pedido é uma **saga** visível, com compensação —
> pagou e não tem motorista, estorna e fecha limpo. Ideia três: **tempo real** — uma ação acende as três
> telas ao vivo. Resultado: **um comando** sobe tudo, o pedido vai de ponta a ponta, testado, e **sem
> licença comercial**. Tá tudo no repositório."

---

# PARTE B — Storyboard (vídeo completo)

Painéis alinhados às cenas do roteiro completo. Cada painel: um frame em ASCII (o que aparece), plano,
texto na tela, áudio resumido e a transição.

### Painel 1 — Hook · DUR 0:00–0:25 · PLANO split-screen (app | diagrama) → corte terminal
```
┌──────────────────────────────┬──────────────────────────────┐
│  [ app: consumidor pedindo ]  │   [ diagrama C4 Container ]   │
│   🍕  Pizza Place   R$ 30     │   spa → gateway → 7 serviços  │
│        [ Pedir ]              │        🗄 🗄 🗄 🗄            │
└──────────────────────────────┴──────────────────────────────┘
        TELA: "Como pensamos a arquitetura — Restaurant Delivery MVP"
```
🎙️ ÁUDIO: "Como você desenharia, do zero, um delivery em microsserviços que sobe com **um comando**?"
→ corte rápido (whip) para o Painel 2.

### Painel 2 — O problema · DUR 0:25–1:20 · PLANO screencast (editor)
```
┌───────────────────────────────────────────────┐
│ prompt.md                                     │
│ "MVP **mockado** ... microsserviços ..."      │
│  ▸ busca ▸ cardápio ▸ pedido ▸ motorista ▸ $  │
└───────────────────────────────────────────────┘
        TELA: "Objetivo: PoC + fundação (não competir com o iFood)"
```
🎙️ ÁUDIO: "'Mockado' é a palavra‑chave: provar a arquitetura, não processar dinheiro."
→ fade.

### Painel 3 — Perguntas de produto · DUR 1:20–2:30 · PLANO motion graphics (cards)
```
┌──────────┐ ┌──────────┐ ┌─────────────────┐ ┌──────────────┐
│ 3 lados  │ │  happy   │ │ PAGAR  →  depois │ │ seletor de   │
│ interativ│ │  path +1 │ │ DESPACHAR motor. │ │ papel s/login│
└──────────┘ └──────────┘ └─────────────────┘ └──────────────┘
        TELA: "Decisão‑chave: pagar → depois despachar"
```
🎙️ ÁUDIO: "Pagamento **antes** do motorista — isso cria, de propósito, a única falha que importa."
→ corte.

### Painel 4 — Seams trocáveis · DUR 2:30–3:40 · PLANO animação + close no código
```
        IPaymentPort
   ┌───────[ porta ]───────┐
   │                       │
 [ Mock ]  ⇄ trocar ⇄  [ Real (Stripe/PIX) ]
   ↑ async: Capture → "aceito" → callback
        TELA: "Ports & Adapters — o seam é o produto (ADR‑001)"
```
🎙️ ÁUDIO: "Trocar o mock pelo real **sem mexer nos vizinhos**. E o pagamento já é async‑shaped."
→ fade.

### Painel 5 — Debate largura×profundidade · DUR 3:40–4:50 · PLANO avatares + balança
```
   🧑‍🔧 Pragmatic  🏛 Architect  📊 Product  😈 Devil's  🧠 Thinker
            ⚖   [ 1 fatia profunda ]──┴──[ 3 lados ]
        TELA: "Decisões viram ADRs"
```
🎙️ ÁUDIO: "'Saga que nunca compensa é saga de mentira.' No fim, **o produto decide** — escolhemos largura."
→ corte.

### Painel 6 — Decomposição · DUR 4:50–6:00 · PLANO diagrama acendendo serviço a serviço
```
                 ┌─────────── Gateway / BFF ───────────┐
   Search·ES   Catalog·Mongo   Order·PG(saga)   Payment·PG
   Dispatch·Redis     Tracking·Redis     Notification·(stateless)
        TELA: "7 serviços + Gateway · dados só por eventos"
```
🎙️ ÁUDIO: "Regra de ouro: **nenhum serviço lê o banco do outro**. Dados cruzam só por eventos."
→ fade.

### Painel 7 — Saga · DUR 6:00–7:30 · PLANO editor (OrderStateMachine) + diagrama de estados
```
 Placed→AwaitingPayment→Paid→Preparing→AwaitingDriver→DriverAssigned→PickedUp→Delivered
                                   └─ DriverUnavailable → Refund → NoDriverRefunded
        TELA: "Saga orquestrada + outbox + idempotência (ADR‑004)"
```
🎙️ ÁUDIO: "Coordenador central, ciclo de vida **visível**, compensação clara. Nada de 'pago e não entregue'."
→ corte.

### Painel 8 — Polyglot · DUR 7:30–8:15 · PLANO ícones de banco por serviço
```
 Order/Payment → 🐘 PostgreSQL   Catalog → 🍃 MongoDB
 Search → 🔎 Elasticsearch       Dispatch/Tracking → 🟥 Redis
        TELA: "Polyglot — o banco certo por serviço (ADR‑006)"
```
🎙️ ÁUDIO: "Cada serviço dono dos seus dados, no banco que faz sentido."
→ fade.

### Painel 9 — Tempo real · DUR 8:15–9:10 · PLANO 3 janelas lado a lado (demo)
```
┌ consumidor ┐ ┌ restaurante ┐ ┌ motorista ┐
│ ●─●─○─○─○  │ │ [Aceitar]✔  │ │ [Retirar] │
│ Preparando │ │             │ │           │
└────────────┘ └─────┬───────┘ └───────────┘
        (clicou "Aceitar" → barra do consumidor acende ao vivo)
        TELA: "SignalR · barra de 5 estágios (ADR‑007)"
```
🎙️ ÁUDIO: "Uma ação acende as três telas em menos de dois segundos. Microsserviço virou experiência."
→ corte.

### Painel 10 — O que deu errado · DUR 9:10–10:40 · PLANO terminal (testes/erros)
```
 $ dotnet test  ▸ E2E ...                         ┌ achou: outbox sem commit ┐
 ✗ OrderAccepted nunca chega ──────────────────────┘ (corrigido: SaveChanges) │
 $ docker compose up  ▸ ✗ "MassTransit License..." → trocar p/ MassTransit 8   │
        TELA: "O E2E achou um bug real · MT9 (comercial) → MT8 (Apache‑2.0)"
```
🎙️ ÁUDIO: "Arquitetura boa **revela** bugs cedo. O E2E pegou o outbox; e o MassTransit 9 virou pago — fomos pro 8."
→ fade.

### Painel 11 — Resultado · DUR 10:40–11:30 · PLANO terminal + curl
```
 $ docker compose up -d --wait      ✔ 14 containers healthy (37s)
 place → settle → accept → ready → pickup → deliver
 GET /status →  stage 5: Delivered  ✅
        TELA: "1 comando · ~300 testes · sem licença"
```
🎙️ ÁUDIO: "Um comando sobe tudo e o pedido vai até **Entregue**. Testado. Sem licença comercial."
→ corte.

### Painel 12 — Fechamento · DUR 11:30–12:00 · PLANO docs + cartela
```
 README (C4) · SYSTEM_DESIGN.md · ADRs
        TELA: "Decisões → ADRs · Arquitetura → SYSTEM_DESIGN.md · obrigado!"
        cartela: github.com/otimize-io/test-compozy-restaurant-delivery
```
🎙️ ÁUDIO: "Comece pelo porquê, vire isso em critério, deixe o produto decidir, e **prove com testes reais**."
→ fim.

---

## 🎨 Notas de arte / motion
- **Paleta:** usar o brand do app (`#ea1d2c`) para destaques/ativos; cinzas neutros para estrutura (ver
  [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md)). Verde `#1ba672` para "concluído/healthy", vermelho para erros.
- **Tipografia:** sem serifa, system‑ui (consistente com o app).
- **Transições:** cortes secos no teaser; fades/whip suaves no vídeo completo.
- **Diagramas:** reaproveitar os C4 do README (renderizar e gravar); animar acendendo elementos.
- **Ritmo:** teaser ~75s (música crescente); vídeo completo ~12 min (música de fundo discreta).
- **Acessibilidade:** legendas PT em ambos.
