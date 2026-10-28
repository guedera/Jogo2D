# Visão técnica — Jogo de digitação versus (LAN)

> Documento vivo. Registra as decisões de design e de engenharia do protótipo e o que precisa ser escalável para a versão final.
> Projeto Unity em `My project/` (Unity `6000.6.6f1`, template 2D URP). Entrega final: **11/11/2026 às 23:50** (GitHub + itch.io + Blackboard). Entrega do protótipo: **quinta-feira, 29/10/2026**.

---

## 1. Resumo do jogo

Jogo **versus de 2 jogadores** em rede local. Quando a partida começa, os dois veem **o mesmo texto de 100 palavras**. Vence quem digitar o texto inteiro, sem nenhum erro, primeiro.

- O texto aparece **cinza** (ainda não digitado). Ao digitar, cada letra vira **branca** (certa) ou **vermelha** (errada).
- A cada **palavra completada corretamente** há **15%** de chance de sortear um poder (3 poderes, ~33% cada).
- O poder sorteado entra numa **fila** com o ícone na tela. **Mouse1** usa o poder que está na frente da fila.

### Poderes

| Poder | Efeito (sobre o adversário) |
|---|---|
| **BOBOMB** | Uma bomba cai na palavra que o adversário está digitando e **apaga as últimas 10 letras** que ele digitou. |
| **GLITCH** | O adversário vê o próprio texto em **leet** por **15 s**. |
| **CHANGE** | O cursor `\|` do adversário é jogado para um **ponto aleatório** do texto digitado. Ele precisa voltar ao final com as **setas**. |

---

## 2. Decisões de design (fechadas)

| # | Tema | Decisão |
|---|---|---|
| 1 | Erro de digitação | **Não bloqueia.** O jogador continua digitando, e o que está errado fica vermelho até ser corrigido. Só fica branco o que está certo. |
| 2 | Inserção no meio do texto | A letra é **inserida** e **empurra o resto** para frente. A verificação é **letra a letra**: depois de uma inserção, tudo que ficou deslocado pode virar vermelho. |
| 3 | Inventário de poderes | **Fila (FIFO).** Poderes sorteados ficam guardados, e o mouse1 gasta o **primeiro da fila**. |
| 4 | Sorteio por palavra | **Cada palavra conta uma vez só.** Apagar e redigitar não gera novo sorteio. |
| 5 | Idioma | **Só inglês, palavras embaralhadas, sem pontuação e sem acentos.** O projeto deve ser **escalável** para português, acentos e outros modos. |
| 6 | O que se vê do adversário | **Só a barra de progresso.** O texto é **igual para os dois** no início de cada partida. |
| 7 | Nº de jogadores | **2** no protótipo, com a arquitetura **escalável para N**. |
| 8 | Entrega do protótipo | Precisa rodar em **build** (não só no editor) em **2 máquinas**. |
| 9 | Plataformas | **2 usuários Mac + 2 Windows.** Compilar para os dois e, se possível, **jogar entre si** (Mac ↔ Windows). |
| 10 | Visual | **Genérico e simples** no protótipo (pode ser feio). |
| 11 | Cor da letra | **Letra a letra:** uma letra certa na posição certa fica **branca**, mesmo depois de um erro anterior. O **prefixo correto** é usado só para progresso e contagem de palavras. |
| 12 | Navegação do cursor | **Só as setas ← →.** Home, End e outras teclas de navegação **não funcionam**. **Segurar a seta não repete:** cada pressão move **uma posição**. |

---

## 3. Arquitetura geral

### 3.1 Princípios

- O jogo é quase todo **UI** (texto, ícones, barras). Não há física nem movimento para sincronizar.
- **Cada cliente digita localmente** e envia só o necessário. O **host é a autoridade** para: texto da partida, sorteio de poderes, vencedor e roteamento dos poderes.
- **Sem acoplar ao "adversário":** o código fala em "alvos" e em "outros jogadores", não em "o outro jogador". Isso deixa o N-jogadores como mudança de configuração.
- **Dados em ScriptableObjects** (textos, poderes, modos) para adicionar conteúdo sem mexer na lógica.

### 3.2 Câmera e cena

- **Uma câmera** ortográfica 2D.
- Canvas em **Screen Space – Camera**, para a bomba e as partículas aparecerem na frente do texto. Cinemachine não é necessário.
- **Duas cenas:**
  - `MainMenu`: Host, Join (campo de IP), Sair.
  - `Game`: espera, contagem 3-2-1, partida e resultado, tudo como painéis.
- Manter lobby e partida na mesma cena evita troca de cena durante a rede e reduz conflitos de merge.

### 3.3 Estados da partida (`GameManager`, no host)

```
Esperando jogadores → Contagem (3-2-1) → Jogando → Fim (vencedor)
        ↑                                                  |
        └──────────── voltar ao menu / revanche ───────────┘
```

---

## 4. Módulos

### 4.1 Digitação (`Typing`)

**Captura de entrada:** Input System (`Keyboard.current.onTextInput` para caracteres; teclas de Backspace e setas tratadas à parte). **Não usar `InputField`**, porque o cursor precisa ser controlado por código. O `onTextInput` já entrega Unicode, então acentos funcionarão no futuro.

**Navegação:** só `←` e `→`, lidas com `wasPressedThisFrame`, que dispara **uma vez por pressão** e ignora a repetição ao segurar a tecla. Home, End, Ctrl+setas, Page Up/Down e o mouse **não movem o cursor**. Ctrl/Cmd+A, C, V e atalhos de colagem também devem ser **ignorados**, para o jogador não colar o texto.

**`TypingModel`** (lógica pura em C#, sem MonoBehaviour, testável):
- `target`: texto alvo.
- `typed`: texto digitado.
- `cursor`: posição do cursor, de `0` até `typed.Length`.
- Operações: `Insert(char)`, `Backspace()`, `MoveLeft()`, `MoveRight()`, `DeleteLast(n)`, `SetCursor(i)`.
- Consultas: `CorrectPrefixLength`, `IsComplete`, `CompletedWordCount`.

**O que é exibido** (`TypingView`):
```
texto exibido = typed  (cada letra branca ou vermelha, mostrando a letra digitada)
              + target[typed.Length..]  (restante em cinza)
```
- Letra `i` do `typed` é **branca** se `typed[i] == target[i]`, senão **vermelha**.
- O cursor `|` pisca na posição `cursor`.
- Se `typed` passar do tamanho de `target`, as letras extras ficam vermelhas, até um limite (constante).
- **Fonte monoespaçada** (TMP Font Asset), para o texto não "dançar" quando uma letra errada tem largura diferente.
- A coloração por caractere é feita com TextMeshPro (cor por vértice, ou tags `<color>` montadas por trecho). Com ~600 caracteres a reconstrução a cada tecla é barata.

**Progresso e contagem de palavras** usam o **prefixo correto** (`CorrectPrefixLength`), não a cor letra a letra. Uma palavra só conta quando todo o prefixo até ela está certo.

**Vitória:** `typed == target` (sem espaço sobrando no final).

### 4.2 Poderes (`Powers`)

- `PowerDefinition` (ScriptableObject): `id`, nome, ícone, e a lógica de aplicação.
- **Adicionar um poder** = criar um novo ScriptableObject + uma classe de efeito, sem mexer no `GameManager`.
- `PowerQueue` por jogador (FIFO). A UI mostra o primeiro ícone grande e os próximos pequenos.
- **Sorteio no host:** quando o cliente avisa "palavra N completada", o host verifica se a palavra N já foi contada, rola 15% e, se cair, sorteia um dos poderes (probabilidade uniforme, configurável) e avisa o cliente.
- **Uso:** mouse1 → cliente envia `UsePower` ao host → o host escolhe os **alvos** (`TargetSelector`, no protótipo "o outro jogador") → envia o efeito ao(s) cliente(s) alvo.

**Efeitos (executados no cliente alvo, que é dono do `TypingModel`):**

| Poder | Implementação |
|---|---|
| BOBOMB | Animação de uma bomba caindo na posição da **palavra atual**, obtida pelas posições de caracteres do TMP (`TMP_TextInfo`). Ao explodir, `DeleteLast(10)` no `typed` (se houver menos de 10, apaga tudo). |
| GLITCH | Por 15 s, a exibição passa por um **mapa de leet** aplicado **só na renderização**. O `typed` e o `target` reais não mudam. |
| CHANGE | `SetCursor(random)` com um valor em `[0, typed.Length)`. O jogador volta com as setas. |

**Regra do leet:** o mapa deve ser **1 caractere → 1 caractere** (`a→4, e→3, i→1, o→0, s→5, t→7`), para os índices continuarem alinhados com a coloração.

### 4.3 Rede (`Networking`)

- **Netcode for GameObjects** + **Unity Transport** (UDP).
- **Topologia:** um jogador é **Host** (servidor + jogador); o outro é **Client** e digita o IP. Porta padrão **7777**; o host escuta em `0.0.0.0`.
- **Aprovação de conexão:** limitada a `maxPlayers` (2 no protótipo), recusando conexões extras.

**O que sincroniza (e como):**

| Dado | Mecanismo |
|---|---|
| Texto da partida | O host sorteia e envia a **string completa** ao conectar/começar. Não enviar só a seed, para não depender de o gerador aleatório ser idêntico entre Mac e Windows. |
| Estado da partida | `NetworkVariable` no `GameManager`. |
| Progresso de cada jogador | `NetworkVariable<int>` (tamanho do prefixo correto), escrita pelo dono, enviada só quando muda. |
| Poder sorteado / efeito | RPCs host → cliente. |
| Fim de jogo | Cliente avisa `Finished` e o **primeiro que chegar ao host vence**. |

**Pontos de teste:** latência na LAN é desprezível, então o "primeiro que chegar ao host" é justo o suficiente para o protótipo.

**Mostrar o IP do host:** pegar o IPv4 local via `System.Net.NetworkInformation` e exibir na tela de espera. Com várias placas de rede (Wi-Fi + VPN), o jogador pode precisar informar a correta.

### 4.4 UI

- Menu: Host, Join (+ campo de IP), Sair.
- Tela de espera: mostra o IP do host e quantos jogadores conectaram.
- Contagem 3-2-1.
- Partida: texto central, **barra de progresso do adversário**, fila de poderes.
- Resultado: vitória/derrota e botão voltar ao menu.

### 4.5 Assets (todos placeholder no protótipo)

- 3 ícones de poder, sprite de bomba, explosão (partículas).
- Sons: tecla, erro, poder sorteado, uso, explosão, vitória/derrota.
- Fonte monoespaçada.
- **Banco de palavras em inglês** (lista de palavras comuns). Se vier de fonte externa, **creditar no `README.md`**, como exige o enunciado.

---

## 5. Escalabilidade (para a versão final)

| Futuro | Como o protótipo se prepara |
|---|---|
| **Português e acentos** | `onTextInput` já é Unicode. O texto vem de um `TextPack` (ScriptableObject) com idioma e lista de palavras. O mapa de leet é por pacote/modo. A fonte monoespaçada precisa cobrir os acentos. |
| **Novos modos de jogo** | `GameModeConfig` (ScriptableObject): `TextPack`, nº de palavras, chance de poder, poderes habilitados, duração do glitch, etc. O `GameManager` lê tudo daí. |
| **Mais de 2 jogadores** | Lista de jogadores dinâmica. Barras de progresso geradas por código. `TargetSelector` isolado (todos os outros / o líder / aleatório). `maxPlayers` em configuração. Nada de `opponent` fixo no código. |
| **Novos poderes** | Novo `PowerDefinition` + efeito, sem alterar o núcleo. |
| **Online (fora da LAN)** | A lógica fica em cima do Netcode, então trocar o transport por Relay é viável. Fora do escopo atual. |

---

## 6. Estrutura de pastas sugerida

```
My project/Assets/_Project/
├── Scenes/            MainMenu, Game
├── Scripts/
│   ├── Core/          GameManager, GameModeConfig, estados
│   ├── Typing/        TypingModel, TypingInput, TypingView
│   ├── Powers/        PowerDefinition, PowerQueue, efeitos, TargetSelector
│   ├── Networking/    ConnectionManager, PlayerNet, helpers de IP
│   └── UI/            Menu, Lobby, HUD, Resultado
├── Prefabs/           PlayerNet, Bomb, PowerIcon, ...
├── ScriptableObjects/ Powers/, GameModes/, TextPacks/
├── Art/  Audio/  Fonts/
```

A lógica de digitação (`TypingModel`) fica **sem dependência de Unity ou rede**, para ser reutilizada, testada e substituída com segurança.

---

## 7. Plano para a quinta-feira

### Escopo do protótipo

**Obrigatório**
- Menu Host/Join por IP, funcionando em LAN entre 2 máquinas.
- Mesmo texto para os dois, digitação com cores, cursor, correção e inserção.
- Barra de progresso do adversário.
- Detecção de vencedor e tela de resultado.
- Os 3 poderes funcionando, com arte placeholder.
- Builds de Windows e Mac.

**Desejável:** sons simples, revanche, efeito de explosão.

**Fora do protótipo:** português/acentos, outros modos, mais de 2 jogadores, descoberta automática de salas, arte final, online fora da LAN.

### Ordem de execução

1. Instalar Netcode for GameObjects e criar a estrutura de pastas e as 2 cenas.
2. **Digitação local** (`TypingModel` + `TypingView`) contra um texto fixo.
3. **Poderes locais** contra um oponente simulado ("dummy").
4. **Rede**: host/join, texto compartilhado, progresso, vencedor.
5. **Poderes pela rede**.
6. UI final, sons, ajustes.
7. **Builds** Windows + Mac e teste entre máquinas.

### Divisão sugerida (4 pessoas)

| Pessoa | Frente |
|---|---|
| A | Digitação (`TypingModel`, entrada, `TypingView`) |
| B | Rede e lobby (conexão, estados, vencedor) |
| C | Poderes (fila, sorteio, 3 efeitos) |
| D | UI, arte placeholder, áudio, builds |

**Regra de git:** trabalhar em **prefabs** e scripts separados. Evitar que duas pessoas editem a **mesma cena** ao mesmo tempo, porque conflitos em cena/prefab (YAML) são difíceis de resolver. Sempre commitar os arquivos `.meta`.

---

## 8. Build e testes em Mac e Windows

- **Todos devem usar a mesma versão da Unity** (`6000.6.6f1`, em `ProjectSettings/ProjectVersion.txt`) e o mesmo pacote de Netcode.
- Cada pessoa compila para o **próprio sistema**. Para compilar Windows a partir do Mac, instalar o módulo **Windows Build Support (Mono)** no Unity Hub.
- Build de Mac: preferir **Universal** (Apple Silicon + Intel), conforme as máquinas do grupo.
- **Mac ↔ Windows funciona** com o mesmo código de rede e as mesmas versões. Se algo divergir, suspeitar primeiro de versão de pacote diferente.
- **Firewall:** liberar UDP 7777 no Windows e no macOS.
- **macOS:** apps não assinados geram aviso do Gatekeeper (botão direito → Abrir). O sistema também pode pedir permissão de **Rede Local** na primeira conexão.
- **Rede:** o Wi-Fi do Insper pode isolar clientes (*AP isolation*). Se a LAN não conectar, usar **hotspot do celular**, roteador próprio ou cabo.
- Teste no editor: **Multiplayer Play Mode** (instâncias virtuais) antes dos builds.

### Checklist de teste entre 2 máquinas

- [ ] Host mostra o IP e o cliente conecta.
- [ ] Os dois veem o mesmo texto.
- [ ] Cores corretas (cinza / branco / vermelho) e cursor.
- [ ] Inserção no meio do texto recolore o que ficou deslocado.
- [ ] Barra de progresso do adversário atualiza.
- [ ] Palavras só sorteiam poder uma vez.
- [ ] Os 3 poderes funcionam nos dois sentidos (host → cliente e cliente → host).
- [ ] Vencedor é o mesmo nas duas telas.
- [ ] Teste Mac ↔ Windows nos dois sentidos.

---

## 9. Riscos

| Risco | Mitigação |
|---|---|
| Prazo curto (quinta) | Escopo mínimo definido acima. Rede entra cedo, não por último. |
| Rede do Insper bloquear a LAN | Testar logo; usar hotspot ou roteador. |
| Conflitos de git em cena/prefab | Frentes separadas, prefabs, uma pessoa por cena. |
| Diferenças de versão (Unity/pacotes) | Fixar a versão e commitar `Packages/manifest.json` e `packages-lock.json`. |
| Texto "dançando" ao digitar | Fonte monoespaçada. |
| Vencedor ambíguo em empate | O host decide pela ordem de chegada. |

---

## 10. Pontos em aberto

*(Resolvidos: cor letra a letra e navegação só com setas sem repetição, ver decisões 11 e 12.)*

1. **Backspace segurado:** repete (apaga continuamente) ou também apaga um por pressão, como as setas? Assumimos que **apagar um por pressão**, igual às setas, até alguém dizer o contrário.
2. **Palavra atingida pela BOBOMB:** assumimos a palavra onde está o **final do texto digitado**, não a do cursor.
3. **Limite de `typed` acima do `target`:** definir um valor (por exemplo +20 caracteres).
4. **Revanche:** incluir no protótipo ou só "voltar ao menu"?
5. **Queda de conexão no meio da partida:** assumimos encerrar e voltar ao menu com uma mensagem.
6. **Fonte do banco de palavras em inglês:** escolher e creditar no `README.md`.
7. **Horário limite da quinta-feira** (para planejar a janela de testes).
