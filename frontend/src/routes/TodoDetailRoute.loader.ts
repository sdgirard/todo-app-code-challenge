import type { LoaderFunctionArgs } from 'react-router'
import { getTodoById } from '../api/generated/todos/todos'
import type { TodoResponse } from '../api/generated/models'

export const todoDetailLoader = async ({ params }: LoaderFunctionArgs): Promise<TodoResponse> => {
  const result = await getTodoById(params.id as string)

  if (result.status !== 200) {
    throw new Response('Todo not found', { status: result.status })
  }

  return result.data
}
