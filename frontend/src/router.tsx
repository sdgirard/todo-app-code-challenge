import { createBrowserRouter } from 'react-router'
import { TodoListRoute } from './routes/TodoListRoute'
import { todoListLoader } from './routes/TodoListRoute.loader'
import { todoListAction } from './routes/TodoListRoute.action'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <TodoListRoute />,
    loader: todoListLoader,
    action: todoListAction,
  },
])
