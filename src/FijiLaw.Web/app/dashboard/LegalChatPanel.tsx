'use client';

import { FormEvent, useEffect, useRef, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError } from '../../lib/api';
import styles from './LegalChatPanel.module.css';

type Conversation = {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
  lastMessage?: string | null;
};

type ChatMessage = {
  id: string;
  conversationId: string;
  role: 'user' | 'assistant';
  content: string;
  provider: string;
  createdAt: string;
};

type Props = {
  balance: number | null;
  onBalanceChange: (balance: number) => void;
};

const CONSENT_KEY = 'fijilaw_qwen_chat_consent_v1';

export default function LegalChatPanel({ balance, onBalanceChange }: Props) {
  const [conversations, setConversations] = useState<Conversation[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [message, setMessage] = useState('');
  const [consented, setConsented] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setConsented(localStorage.getItem(CONSENT_KEY) === 'accepted');
    void loadConversations();
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, sending]);

  function accessToken() {
    return sessionStorage.getItem('fijilaw_access_token');
  }

  function authHeaders() {
    const token = accessToken();
    return { Authorization: `Bearer ${token ?? ''}` };
  }

  async function loadConversations(selectFirst = false) {
    setLoadingHistory(true);
    try {
      const response = await fetchWithTimeout(`${API_BASE}/api/chat/conversations`, { headers: authHeaders(), cache: 'no-store' }, 12000);
      if (response.status === 401) {
        sessionStorage.removeItem('fijilaw_access_token');
        window.location.href = '/account?mode=login';
        return;
      }
      if (!response.ok) throw new Error(await readApiError(response, 'Chat history could not be loaded.'));
      const body = await response.json();
      const items: Conversation[] = body.items ?? [];
      setConversations(items);
      if (selectFirst && !activeId && items[0]) await openConversation(items[0].id);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Chat history could not be loaded.');
    } finally {
      setLoadingHistory(false);
    }
  }

  async function openConversation(id: string) {
    setError('');
    setActiveId(id);
    setLoadingHistory(true);
    try {
      const response = await fetchWithTimeout(`${API_BASE}/api/chat/conversations/${id}`, { headers: authHeaders(), cache: 'no-store' }, 12000);
      if (!response.ok) throw new Error(await readApiError(response, 'Conversation could not be loaded.'));
      const body = await response.json();
      setMessages(body.messages ?? []);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Conversation could not be loaded.');
    } finally {
      setLoadingHistory(false);
    }
  }

  function startNewConversation() {
    setActiveId(null);
    setMessages([]);
    setMessage('');
    setError('');
  }

  function updateConsent(accepted: boolean) {
    setConsented(accepted);
    if (accepted) localStorage.setItem(CONSENT_KEY, 'accepted');
    else localStorage.removeItem(CONSENT_KEY);
  }

  async function send(event: FormEvent) {
    event.preventDefault();
    const content = message.trim();
    if (!content || sending) return;
    if (!consented) {
      setError('Accept the data-processing notice before sending a legal question.');
      return;
    }

    setSending(true);
    setError('');
    try {
      const response = await fetchWithTimeout(`${API_BASE}/api/chat/messages`, {
        method: 'POST',
        headers: { ...authHeaders(), 'Content-Type': 'application/json' },
        body: JSON.stringify({ conversationId: activeId, message: content, qwenDataProcessingConsent: true })
      }, 60000);
      if (response.status === 401) {
        sessionStorage.removeItem('fijilaw_access_token');
        window.location.href = '/account?mode=login';
        return;
      }
      if (!response.ok) throw new Error(await readApiError(response, 'FijiLaw AI could not respond.'));
      const body = await response.json();
      const exchange: ChatMessage[] = body.messages ?? [];
      setMessages(current => [...current, ...exchange]);
      setMessage('');
      setActiveId(body.conversation.id);
      if (typeof body.wallet?.balance === 'number') onBalanceChange(body.wallet.balance);
      await loadConversations();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'FijiLaw AI could not respond.');
    } finally {
      setSending(false);
    }
  }

  return (
    <section className={styles.workspace}>
      <aside className={styles.history}>
        <div className={styles.historyHeader}>
          <div>
            <span>PRIVATE WORKSPACE</span>
            <h3>Chat history</h3>
          </div>
          <button type="button" onClick={startNewConversation}>New chat</button>
        </div>
        {loadingHistory && conversations.length === 0 ? <p className={styles.muted}>Loading history...</p> : null}
        {!loadingHistory && conversations.length === 0 ? <p className={styles.muted}>Your conversations will appear here after your first question.</p> : null}
        <div className={styles.conversationList}>
          {conversations.map(item => (
            <button type="button" key={item.id} className={activeId === item.id ? styles.selected : ''} onClick={() => void openConversation(item.id)}>
              <strong>{item.title}</strong>
              <span>{new Date(item.updatedAt).toLocaleDateString()} · {item.messageCount} messages</span>
            </button>
          ))}
        </div>
      </aside>

      <div className={styles.chat}>
        <header className={styles.chatHeader}>
          <div>
            <p>FIJI LEGAL INFORMATION</p>
            <h2>{activeId ? conversations.find(item => item.id === activeId)?.title ?? 'Legal conversation' : 'Start a legal conversation'}</h2>
          </div>
          <span className={styles.creditBadge}>{balance ?? '—'} credits</span>
        </header>

        <div className={styles.messages} aria-live="polite">
          {messages.length === 0 ? (
            <div className={styles.welcome}>
              <span className={styles.mark}>FL</span>
              <h3>Describe the legal issue in your own words.</h3>
              <p>FijiLaw AI will produce source-conscious guidance, identify missing facts, and recommend when human legal review is needed.</p>
              <div className={styles.promptGrid}>
                <button type="button" onClick={() => setMessage('I received a legal notice and need help understanding my next steps.')}>Understand a notice</button>
                <button type="button" onClick={() => setMessage('Help me identify the important facts and documents for my Fiji legal issue.')}>Prepare my facts</button>
              </div>
            </div>
          ) : messages.map(item => (
            <article key={item.id} className={item.role === 'user' ? styles.userMessage : styles.assistantMessage}>
              <div className={styles.messageMeta}>
                <strong>{item.role === 'user' ? 'You' : 'FijiLaw AI'}</strong>
                <span>{new Date(item.createdAt).toLocaleString()}</span>
              </div>
              <p>{item.content}</p>
            </article>
          ))}
          {sending ? <article className={styles.assistantMessage}><div className={styles.thinking}><i /><i /><i /> Reviewing the issue and verified sources...</div></article> : null}
          <div ref={endRef} />
        </div>

        <div className={styles.composer}>
          <label className={styles.consent}>
            <input type="checkbox" checked={consented} onChange={event => updateConsent(event.target.checked)} />
            <span>I agree that this chat will be sent to Alibaba Cloud Qwen in Singapore for processing and stored in my private FijiLaw account history. Do not enter information you are not authorised to share.</span>
          </label>
          {error ? <div className={styles.error}>{error} {error.toLowerCase().includes('credit') ? <a href="/credits">Buy credits</a> : null}</div> : null}
          <form onSubmit={send} className={styles.form}>
            <textarea value={message} onChange={event => setMessage(event.target.value)} maxLength={8000} rows={3} placeholder="Describe your Fiji legal question..." disabled={sending} />
            <div className={styles.formFooter}>
              <span>3 FijiLaw Credits per response · AI legal information, not legal representation</span>
              <button type="submit" disabled={sending || !message.trim() || !consented}>{sending ? 'Working...' : 'Send securely'}</button>
            </div>
          </form>
        </div>
      </div>
    </section>
  );
}
