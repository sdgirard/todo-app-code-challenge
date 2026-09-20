import type { TodoResponse } from '../api/generated/models'

interface TodoDetailProps {
  todo: TodoResponse
}

const formatDate = (value: string | null) => {
  if (!value) return null
  return new Date(value).toLocaleString()
}

export const TodoDetail = ({ todo }: TodoDetailProps) => (
  <div className="flex flex-col gap-4">
    <h1 className="text-2xl font-semibold text-gray-900">{todo.title}</h1>

    <p className="text-sm text-gray-700">{todo.description ?? 'No description'}</p>

    <dl className="flex flex-col gap-2 text-sm">
      <div className="flex justify-between">
        <dt className="text-gray-500">Due Date</dt>
        <dd className="text-gray-900">{formatDate(todo.dueDate) ?? 'No due date'}</dd>
      </div>

      <div className="flex justify-between">
        <dt className="text-gray-500">Status</dt>
        <dd className={todo.isCompleted ? 'text-green-600' : 'text-gray-400'}>
          {todo.isCompleted ? 'Completed' : 'Incomplete'}
        </dd>
      </div>

      <div className="flex justify-between">
        <dt className="text-gray-500">Created At</dt>
        <dd className="text-gray-900">{formatDate(todo.createdAt)}</dd>
      </div>

      <div className="flex justify-between">
        <dt className="text-gray-500">Updated At</dt>
        <dd className="text-gray-900">{formatDate(todo.updatedAt) ?? 'Never updated'}</dd>
      </div>
    </dl>
  </div>
)
