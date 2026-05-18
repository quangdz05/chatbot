import { useEffect, useMemo, useRef, useState } from 'react'
import './App.css'

import { chatService } from './services/chatService'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5119'
const WELCOME_TEXT =
  'Xin chào! Tôi là trợ lý tư vấn. Bạn có thể hỏi tôi về nội dung trong tài liệu đã được cung cấp.'
const API_ERROR_TEXT =
  'Xin lỗi, hiện tại tôi chưa kết nối được tới máy chủ. Vui lòng kiểm tra API backend.'
const GREETING_REPLY_TEXT =
  'Xin chào! Tôi là trợ lý tư vấn. Bạn cần tôi hỗ trợ nội dung nào trong tài liệu?'
const UNCLEAR_SHORT_REPLY_TEXT =
  'Bạn vui lòng nhập câu hỏi rõ hơn để tôi có thể hỗ trợ nhé.'
const GREETING_INPUTS = new Set([
  'hi',
  'hello',
  'hey',
  'chao',
  'xin chao',
  'chao ban',
  'alo',
  'a',
  'hi bot',
  'hello bot',
])

function normalizeIntentText(input) {
  return input
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/đ/g, 'd')
}

function toPlainWords(input) {
  return input.replace(/[^\p{L}\p{N}\s]/gu, ' ').replace(/\s+/g, ' ').trim()
}

function isGreetingMessage(input) {
  const normalized = normalizeIntentText(input)
  const plain = toPlainWords(normalized)
  return GREETING_INPUTS.has(plain)
}

function isUnclearShortMessage(input) {
  const compact = input.replace(/\s+/g, '')
  if (compact.length > 3) {
    return false
  }

  return /^[.?!,;:]+$/.test(compact)
}

function createWelcomeMessage() {
  return {
    role: 'assistant',
    text: WELCOME_TEXT,
    time: new Date().toISOString(),
    isWelcome: true,
    sources: [],
  }
}

function splitLongParagraph(text) {
  if (text.length < 220) {
    return [text]
  }

  const sentences =
    text
      .match(/[^.!?]+[.!?]?/g)
      ?.map((segment) => segment.trim())
      .filter(Boolean) || []

  if (sentences.length <= 2) {
    return [text]
  }

  const chunks = []
  for (let index = 0; index < sentences.length; index += 2) {
    chunks.push(sentences.slice(index, index + 2).join(' ').trim())
  }

  return chunks.filter(Boolean)
}

function buildAssistantBlocks(text) {
  const lines = text.replace(/\r\n/g, '\n').split('\n')
  const blocks = []
  let paragraphBuffer = []
  let listBuffer = []

  function flushParagraph() {
    if (paragraphBuffer.length === 0) {
      return
    }

    const paragraphText = paragraphBuffer.join(' ').trim()
    splitLongParagraph(paragraphText).forEach((piece) => {
      blocks.push({ type: 'paragraph', content: piece })
    })
    paragraphBuffer = []
  }

  function flushList() {
    if (listBuffer.length === 0) {
      return
    }

    blocks.push({ type: 'list', items: listBuffer })
    listBuffer = []
  }

  lines.forEach((line) => {
    const trimmed = line.trim()

    if (!trimmed) {
      flushParagraph()
      flushList()
      return
    }

    const bulletMatch = trimmed.match(/^([-*•]|\d+\.)\s+(.+)$/)
    if (bulletMatch) {
      flushParagraph()
      listBuffer.push(bulletMatch[2].trim())
      return
    }

    flushList()
    paragraphBuffer.push(trimmed)
  })

  flushParagraph()
  flushList()

  if (blocks.length === 0) {
    return [{ type: 'paragraph', content: text }]
  }

  return blocks
}

function App() {
  const search = useMemo(() => new URLSearchParams(window.location.search), [])
  const embedded = search.get('embed') === '1'

  const [isOpen, setIsOpen] = useState(embedded)
  const [message, setMessage] = useState('')
  const [conversationId, setConversationId] = useState('')
  const [chatLoading, setChatLoading] = useState(false)
  const [chatHistory, setChatHistory] = useState(() => [createWelcomeMessage()])

  const messagesRef = useRef(null)

  useEffect(() => {
    if (!embedded && !isOpen) {
      return
    }

    const container = messagesRef.current
    if (container) {
      container.scrollTop = container.scrollHeight
    }
  }, [chatHistory, chatLoading, embedded, isOpen])

  async function submitMessage(rawText) {
    const trimmed = rawText.trim()
    if (!trimmed || chatLoading) {
      return
    }

    setMessage('')

    const userEntry = {
      role: 'user',
      text: trimmed,
      time: new Date().toISOString(),
    }
    setChatHistory((prev) => [...prev, userEntry])

    if (isGreetingMessage(trimmed)) {
      const greetingEntry = {
        role: 'assistant',
        text: GREETING_REPLY_TEXT,
        time: new Date().toISOString(),
        sources: [],
      }
      setChatHistory((prev) => [...prev, greetingEntry])
      return
    }

    if (isUnclearShortMessage(trimmed)) {
      const unclearEntry = {
        role: 'assistant',
        text: UNCLEAR_SHORT_REPLY_TEXT,
        time: new Date().toISOString(),
        sources: [],
      }
      setChatHistory((prev) => [...prev, unclearEntry])
      return
    }

    setChatLoading(true)

    try {
      const data = await chatService.ask(API_BASE_URL, trimmed, conversationId)
      if (data?.conversationId) {
        setConversationId(data.conversationId)
      }

      const botEntry = {
        role: 'assistant',
        text: data?.answer || '(No answer)',
        fromKnowledgeBase: Boolean(data?.isFromKnowledgeBase),
        sources: Array.isArray(data?.sources) ? data.sources : [],
        time: new Date().toISOString(),
      }

      setChatHistory((prev) => [...prev, botEntry])
    } catch {
      const fallbackEntry = {
        role: 'assistant',
        text: API_ERROR_TEXT,
        time: new Date().toISOString(),
        sources: [],
      }
      setChatHistory((prev) => [...prev, fallbackEntry])
    } finally {
      setChatLoading(false)
    }
  }

  function sendMessage(event) {
    event.preventDefault()
    submitMessage(message)
  }

  function resetConversation() {
    setConversationId('')
    setChatHistory([createWelcomeMessage()])
    setMessage('')
  }

  return (
    <div className={`app-shell ${embedded ? 'embedded' : ''}`}>
      {!embedded && (
        <main className="landing-page">
          <section className="landing-card">
            <h1>Hệ thống tư vấn thông minh</h1>
            <p>Chatbot hỗ trợ trả lời câu hỏi dựa trên tài liệu nội bộ.</p>
            <div className="feature-list">
              <span className="feature-chip">Hỏi đáp theo tài liệu</span>
              <span className="feature-chip">Trả lời nhanh</span>
              <span className="feature-chip">Hỗ trợ 24/7</span>
            </div>
          </section>
        </main>
      )}

      <div className={`widget-host ${embedded ? 'embedded' : ''}`}>
        {!embedded && !isOpen && (
          <button className="launcher" type="button" onClick={() => setIsOpen(true)}>
            <span className="launcher-icon" aria-hidden="true">
              🤖
            </span>
            <span>Chat hỗ trợ</span>
          </button>
        )}

        {(isOpen || embedded) && (
          <section className="widget-panel">
            <header className="widget-header">
              <div className="header-brand">
                <div className="bot-avatar" aria-hidden="true">
                  🤖
                </div>
                <div>
                  <p className="widget-title">Trợ lý tư vấn</p>
            
                </div>
              </div>
              <div className="header-actions">
                <button className="ghost" type="button" onClick={resetConversation} title="Cuộc trò chuyện mới">
                  ⟳
                </button>
                {!embedded && (
                  <button className="ghost" type="button" onClick={() => setIsOpen(false)} title="Thu nhỏ">
                    −
                  </button>
                )}
              </div>
            </header>

            <div className="messages" ref={messagesRef}>
              {chatHistory.map((item, index) => (
                <article key={`${item.time}-${index}`} className={`bubble-row ${item.role}`}>
                  <div className="bubble-meta">{item.role === 'assistant' ? 'Bot' : 'Bạn'}</div>
                  <div className={`bubble ${item.role}`}>
                    {item.role === 'assistant'
                      ? buildAssistantBlocks(item.text).map((block, blockIndex) => {
                          if (block.type === 'list') {
                            return (
                              <ul key={`${item.time}-list-${blockIndex}`}>
                                {block.items.map((listItem, listItemIndex) => (
                                  <li key={`${item.time}-list-${blockIndex}-${listItemIndex}`}>{listItem}</li>
                                ))}
                              </ul>
                            )
                          }

                          return <p key={`${item.time}-paragraph-${blockIndex}`}>{block.content}</p>
                        })
                      : <p>{item.text}</p>}

                    {item.role === 'assistant' && item.sources?.length > 0 && (
                      <details>
                        <summary>Nguồn tham chiếu ({item.sources.length})</summary>
                        <ul>
                          {item.sources.map((source) => (
                            <li key={`${source.documentId}-${source.pageNumber}-${source.heading}`}>
                              <strong>{source.fileName}</strong>
                              <span> - Trang {source.pageNumber}</span>
                            </li>
                          ))}
                        </ul>
                      </details>
                    )}
                  </div>
                </article>
              ))}

              {chatLoading && (
                <article className="bubble-row assistant">
                  <div className="bubble-meta">Bot</div>
                  <div className="bubble assistant typing">Đang trả lời...</div>
                </article>
              )}
            </div>

            <form onSubmit={sendMessage} className="composer">
              <input
                value={message}
                onChange={(event) => setMessage(event.target.value)}
                placeholder={chatLoading ? 'Đang trả lời...' : 'Nhập câu hỏi...'}
                disabled={chatLoading}
              />
              <button type="submit" disabled={chatLoading || !message.trim()} aria-label="Gửi câu hỏi">
                ➤
              </button>
            </form>
          </section>
        )}
      </div>
    </div>
  )
}

export default App
