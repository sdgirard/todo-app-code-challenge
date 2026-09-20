import { useState } from 'react'

interface Todo {
  id: string
  title: string
  completed: boolean
}

function App() {
  const [todos, setTodos] = useState<Todo[]>([])
  const [title, setTitle] = useState('')

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const trimmedTitle = title.trim()
    if (!trimmedTitle) return

    setTodos((current) => [
      ...current,
      { id: crypto.randomUUID(), title: trimmedTitle, completed: false },
    ])
    setTitle('')
  }

  function toggleTodo(id: string) {
    setTodos((current) =>
      current.map((todo) =>
        todo.id === id ? { ...todo, completed: !todo.completed } : todo,
      ),
    )
  }

  function deleteTodo(id: string) {
    setTodos((current) => current.filter((todo) => todo.id !== id))
  }

  return (
    <main className="mx-auto max-w-md p-6">
      <h1 className="mb-4 text-2xl font-semibold text-gray-900">To-Do List</h1>

      <form onSubmit={handleSubmit} className="mb-6 flex gap-2">
        <label htmlFor="new-todo-title" className="sr-only">
          New to-do title
        </label>
        <input
          id="new-todo-title"
          type="text"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="What needs doing?"
          className="flex-1 rounded border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none"
        />
        <button
          type="submit"
          className="rounded bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Add
        </button>
      </form>

      {todos.length === 0 ? (
        <p className="text-sm text-gray-500">No to-dos yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {todos.map((todo) => (
            <li
              key={todo.id}
              className="flex items-center gap-3 rounded border border-gray-200 px-3 py-2"
            >
              <input
                type="checkbox"
                checked={todo.completed}
                onChange={() => toggleTodo(todo.id)}
                aria-label={`Mark "${todo.title}" as ${todo.completed ? 'incomplete' : 'complete'}`}
              />
              <span
                className={`flex-1 text-sm ${todo.completed ? 'text-gray-400 line-through' : 'text-gray-900'}`}
              >
                {todo.title}
              </span>
              <button
                type="button"
                onClick={() => deleteTodo(todo.id)}
                className="text-sm text-red-600 hover:text-red-800"
                aria-label={`Delete "${todo.title}"`}
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}

export default App
