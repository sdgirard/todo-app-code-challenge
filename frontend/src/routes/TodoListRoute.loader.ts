import { listTodos } from '../api/generated/todos/todos'
import type { TodoResponse } from '../api/generated/models'

export const todoListLoader = async (): Promise<TodoResponse[]> => {
  const result = await listTodos()

  if (result.status !== 200) {
    throw new Response('Failed to load todos', { status: result.status })
  }

  return result.data
}
