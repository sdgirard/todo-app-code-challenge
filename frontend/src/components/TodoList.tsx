import { Link } from 'react-router'
import type { TodoResponse } from '../api/generated/models'

interface TodoListProps {
  todos: TodoResponse[]
}

const formatDueDate = (dueDate: string | null) => {
  if (!dueDate) return 'No due date'
  return new Date(dueDate).toLocaleString()
}

export const TodoList = ({ todos }: TodoListProps) => {
  if (todos.length === 0) {
    return <p className="text-sm text-gray-500">No to-dos yet.</p>
  }

  return (
    <ul className="flex flex-col gap-2">
      {todos.map((todo) => (
        <li
          key={todo.id}
          className="flex items-center gap-3 rounded border border-gray-200 px-3 py-2"
        >
          <Link to={`/todos/${todo.id}`} className="flex-1 text-sm text-gray-900 hover:underline">
            {todo.title}
          </Link>
          <span className="text-xs text-gray-500">{formatDueDate(todo.dueDate)}</span>
          <span
            className={`text-xs font-medium ${todo.isCompleted ? 'text-green-600' : 'text-gray-400'}`}
          >
            {todo.isCompleted ? 'Completed' : 'Incomplete'}
          </span>
        </li>
      ))}
    </ul>
  )
}
