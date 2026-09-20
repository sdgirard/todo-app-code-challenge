import { redirect, type ActionFunctionArgs } from 'react-router'
import { deleteTodo } from '../api/generated/todos/todos'

export const todoDetailAction = async ({ params, request }: ActionFunctionArgs): Promise<Response> => {
  const formData = await request.formData()
  const intent = formData.get('intent')

  if (intent !== 'delete') {
    throw new Response('Unknown action', { status: 400 })
  }

  // Both documented outcomes (204 deleted, 404 already gone) satisfy the
  // user's intent that this todo no longer exist — see spec's Action section.
  await deleteTodo(params.id as string)
  return redirect('/')
}
